"""Core evaluation loop.

For each (task, model) pair we run two agents:
  - sequential: Python REPL only, no cluster knowledge
  - parallel:   PARCS cluster agent

Results are appended to a shared CSV after every run.
"""

from __future__ import annotations

import asyncio
import csv
import json
import re
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Literal

from rich.console import Console
from rich.live import Live
from rich.panel import Panel

from .agent import create_parcs_agent, create_sequential_agent
from .config import config
from .console import AgentDisplay
from .tasks import BenchmarkTask, make_parallel_prompt, make_sequential_prompt


Mode = Literal["sequential", "parallel"]

RESULT_FIELDS = [
    "run_id",
    "timestamp",
    "model",
    "task_id",
    "mode",               # "sequential" | "parallel"
    "elapsed_seconds",    # computation time (python_exec or PARCS totalElapsedSeconds)
    "parallelism_used",   # blank for sequential
    "result_summary",
    "agent_error",
    "wall_clock_seconds", # total wall time including LLM calls
]

_JSON_FENCE = re.compile(r"```json\s*(\{.*?\})\s*```", re.DOTALL)


def _extract_result_json(text: str) -> dict[str, Any] | None:
    matches = _JSON_FENCE.findall(text)
    if not matches:
        return None
    try:
        return json.loads(matches[-1])
    except json.JSONDecodeError:
        return None


def _collect_final_text(messages: list) -> str:
    from langchain_core.messages import AIMessage
    for msg in reversed(messages):
        if isinstance(msg, AIMessage):
            content = msg.content
            if isinstance(content, str):
                return content
            if isinstance(content, list):
                return "\n".join(
                    p.get("text", "") for p in content
                    if isinstance(p, dict) and p.get("type") == "text"
                )
    return ""


async def _stream_agent(agent, prompt: str, display: AgentDisplay, console: Console) -> list:
    """Run the agent, stream to console, return full message list."""
    messages: list = []
    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", prompt)]},
            stream_mode="values",
        ):
            messages = chunk.get("messages") or []
            new = messages[display.printed_count:]
            if not new:
                continue
            live.stop()
            for msg in new:
                display.print_message(msg)
            display.printed_count = len(messages)
            live.update(display.spinner)
            live.start()
    return messages


async def run_task(
    task: BenchmarkTask,
    model_name: str,
    mode: Mode,
    console: Console,
) -> dict[str, Any]:
    run_id = f"task{task.task_id}_{mode}_{model_name.replace('/', '-')}_{int(time.time())}"

    mode_color = "cyan" if mode == "parallel" else "yellow"
    console.print(
        Panel(
            f"[bold]Task {task.task_id}[/]  mode=[{mode_color}]{mode}[/]  model=[white]{model_name}[/]",
            border_style=mode_color,
        )
    )

    if mode == "parallel":
        agent = await create_parcs_agent(model_name)
        prompt = make_parallel_prompt(task)
    else:
        agent = create_sequential_agent(model_name)
        prompt = make_sequential_prompt(task)

    display = AgentDisplay(console)
    wall_start = time.monotonic()
    messages: list = []
    agent_error = ""

    try:
        async with asyncio.timeout(config.eval.timeout_seconds):
            messages = await _stream_agent(agent, prompt, display, console)
    except TimeoutError:
        agent_error = f"timeout after {config.eval.timeout_seconds}s"
        console.print("[red]✗ Timed out[/]")
    except Exception as exc:  # noqa: BLE001
        agent_error = str(exc)
        console.print(f"[red]✗ Error: {exc}[/]")

    wall_seconds = round(time.monotonic() - wall_start, 2)
    final_text = _collect_final_text(messages)
    parsed = _extract_result_json(final_text) or {}

    result: dict[str, Any] = {
        "run_id": run_id,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "model": model_name,
        "task_id": task.task_id,
        "mode": mode,
        "elapsed_seconds": parsed.get("elapsed_seconds", ""),
        "parallelism_used": parsed.get("parallelism_used", ""),
        "result_summary": parsed.get("result_summary", ""),
        "agent_error": agent_error,
        "wall_clock_seconds": wall_seconds,
    }

    _log_result(result, console)
    return result


def _log_result(r: dict[str, Any], console: Console) -> None:
    if r["agent_error"]:
        console.print(f"  [red]Error: {r['agent_error']}[/]")
        return
    elapsed = r.get("elapsed_seconds", "?")
    mode_color = "cyan" if r["mode"] == "parallel" else "yellow"
    console.print(
        f"  [{mode_color}]{r['mode']}[/] elapsed=[bold]{elapsed}s[/]  "
        f"summary: {r.get('result_summary', '')}"
    )


# ── CSV helpers ────────────────────────────────────────────────────────────────

def _ensure_csv(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        with path.open("w", newline="", encoding="utf-8") as f:
            csv.DictWriter(f, fieldnames=RESULT_FIELDS).writeheader()


def append_result(result: dict[str, Any], path: Path) -> None:
    _ensure_csv(path)
    with path.open("a", newline="", encoding="utf-8") as f:
        csv.DictWriter(f, fieldnames=RESULT_FIELDS, extrasaction="ignore").writerow(result)


def load_completed(path: Path) -> set[tuple[int, str, str]]:
    """Return (task_id, model, mode) triples that already succeeded."""
    if not path.exists():
        return set()
    completed: set[tuple[int, str, str]] = set()
    with path.open(newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            if not row.get("agent_error") and row.get("elapsed_seconds"):
                completed.add((int(row["task_id"]), row["model"], row["mode"]))
    return completed


# ── Main benchmark loop ────────────────────────────────────────────────────────

async def run_benchmark(
    tasks: list[BenchmarkTask],
    models: list[str],
    modes: list[Mode],
    results_path: Path,
    console: Console,
    skip_completed: bool = True,
) -> None:
    completed = load_completed(results_path) if skip_completed else set()

    total = len(tasks) * len(models) * len(modes)
    done = 0

    for model in models:
        for mode in modes:
            for task in tasks:
                done += 1
                if skip_completed and (task.task_id, model, mode) in completed:
                    console.print(
                        f"[dim]Skipping task {task.task_id} / {mode} / {model}[/]"
                    )
                    continue

                console.print(
                    f"\n[bold white]── [{done}/{total}] "
                    f"Task {task.task_id} · {mode} · {model} ──[/]"
                )
                result = await run_task(task, model, mode, console)
                append_result(result, results_path)

    console.print(f"\n[bold green]✓ All done. Results: {results_path}[/]")
