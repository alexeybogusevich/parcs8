import asyncio
import json
import time
from argparse import ArgumentParser
from datetime import datetime, timezone
from uuid import uuid4

from rich.console import Console
from rich.live import Live

from mcp_eval.agent import create_parcs_agent
from mcp_eval.config import config
from mcp_eval.console import AgentDisplay
from mcp_eval.tools import CallLog


DEFAULT_TASK = "You have a portfolio of 50 assets with equal weights (w_i = 1/50). The return covariance matrix Σ ∈ ℝ^{50×50} is generated from seed=42: A = randn(50,50,seed=42); Σ = Aᵀ·A/50 + 0.01·I. Using 2,000,000 Monte Carlo scenarios (each of 20 workers generates 100,000 using seed WorkerIndex*1000+42 and the shared Cholesky factor L of Σ), estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of the portfolio loss distribution. Loss = –(portfolio return). Return {var_99, cvar_99} as floats."

SELF_REFLECTION_TASK = "Now, reflect on the job you've just completed and find important insights that will help you to perform better in the future. Modify your memory: /skills/parcs-cluster/SKILL.md and /memory/AGENTS.md file."


def parse_args():
    parser = ArgumentParser(description="Run the Parcs MCP Agent")
    parser.add_argument(
        "--task",
        type=str,
        help="The task to be performed by the agent",
        default=DEFAULT_TASK,
    )
    parser.add_argument(
        "--orchestration",
        choices=["direct", "ptc"],
        default=None,
        help="direct: the model calls PARCS tools turn by turn; ptc: it writes JavaScript that "
             "calls them (default: PTC__ORCHESTRATION from .env, else direct)",
    )
    parser.add_argument(
        "--no-reflection",
        action="store_true",
        help="Skip the self-reflection pass that edits SKILL.md and AGENTS.md (use for measured runs)",
    )
    return parser.parse_args()


async def stream(agent, text: str, thread_id: str, display: AgentDisplay, console: Console) -> list:
    messages: list = []
    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", text)]},
            config={"configurable": {"thread_id": thread_id}},
            stream_mode="values",
        ):
            messages = chunk.get("messages") or []
            new_messages = messages[display.printed_count :]
            if not new_messages:
                continue

            live.stop()
            for msg in new_messages:
                display.print_message(msg)
            display.printed_count = len(messages)
            live.update(display.spinner)
            live.start()
    return messages


def run_metrics(messages: list, log: CallLog) -> dict:
    """Model turns, billed tokens and interpreter use of one run, plus the PARCS call log summary."""
    from langchain_core.messages import AIMessage, ToolMessage

    metrics = {"model_turns": 0, "input_tokens": 0, "output_tokens": 0, "evals": 0, "eval_errors": 0}
    for msg in messages:
        if isinstance(msg, AIMessage):
            metrics["model_turns"] += 1
            usage = getattr(msg, "usage_metadata", None) or {}
            metrics["input_tokens"] += usage.get("input_tokens", 0)
            metrics["output_tokens"] += usage.get("output_tokens", 0)
        elif isinstance(msg, ToolMessage) and msg.name == "eval":
            metrics["evals"] += 1
            if getattr(msg, "status", None) == "error" or "<error" in str(msg.content):
                metrics["eval_errors"] += 1
    return metrics | log.summary()


async def main() -> None:
    args = parse_args()
    orchestration = args.orchestration or config.ptc.orchestration

    log = CallLog()
    agent = await create_parcs_agent(orchestration, log)
    console = Console()
    display = AgentDisplay(console)

    console.print(f"[dim]orchestration={orchestration}  model={config.llm.model_name}[/]")
    console.print()

    thread_id = str(uuid4())
    started = time.monotonic()
    messages = await stream(agent, args.task, thread_id, display, console)
    wall_seconds = round(time.monotonic() - started, 2)

    console.print()
    console.print("[bold green]✓ Done![/]")

    record = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "thread_id": thread_id,
        "orchestration": orchestration,
        "model": config.llm.model_name,
        "task": args.task[:200],
        "wall_clock_seconds": wall_seconds,
        "max_result_chars": config.ptc.max_result_chars,
        "preload_skill": config.agent.preload_skill,
        **run_metrics(messages, log),
        "calls": [c.__dict__ for c in log.calls],
    }
    runs_file = config.paths.results_dir / "runs.jsonl"
    runs_file.parent.mkdir(parents=True, exist_ok=True)
    with runs_file.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")
    summary = {k: v for k, v in record.items() if k not in ("calls", "task")}
    console.print(f"[dim]{json.dumps(summary)}[/]")
    console.print(f"[dim]run log → {runs_file}[/]")

    if args.no_reflection:
        return

    await stream(agent, SELF_REFLECTION_TASK, thread_id, display, console)

    console.print()
    console.print("[bold green]✓ Reflection Complete![/]")


if __name__ == "__main__":
    asyncio.run(main())
