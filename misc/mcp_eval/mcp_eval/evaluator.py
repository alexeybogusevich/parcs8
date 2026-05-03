# """Core evaluation loop.

# For each (task, model) pair we run two agents:
#   - sequential: Python REPL only, no cluster knowledge
#   - parallel:   PARCS cluster agent

# Results are appended to a shared CSV after every run.
# """

# from __future__ import annotations

# import asyncio
# import csv
# import json
# import re
# import time
# from datetime import datetime, timezone
# from pathlib import Path
# from typing import Any, Literal

# from rich.console import Console
# from rich.live import Live
# from rich.panel import Panel

# from .agent import create_parcs_agent, create_sequential_agent
# from .config import config
# from .console import AgentDisplay
# from .tasks import BenchmarkTask, make_parallel_prompt, make_sequential_prompt


# Mode = Literal["sequential", "parallel"]

# RESULT_FIELDS = [
#     # ── Task identity ────────────────────────────────────────────────────
#     "run_id",
#     "timestamp",
#     "model",
#     "task_id",
#     "task_title",           # human-readable name, e.g. "Monte Carlo VaR"
#     "task_domain",          # research domain, e.g. "Quantitative Finance"
#     "task_question",        # full prompt given to the agent (first 600 chars)
#     "task_reference_answer",# reference answer JSON from the benchmark split
#     "mode",                 # sequential | parallel
#     "status",               # completed | failed | timeout

#     # ── LLM usage ────────────────────────────────────────────────────────
#     "llm_turns",            # number of model invocations (reasoning steps)
#     "input_tokens",
#     "output_tokens",
#     "total_tokens",

#     # ── Tool call breakdown ───────────────────────────────────────────────
#     "create_session_attempts",  # includes retries on compile error
#     "compilation_failures",     # sessions that failed to compile
#     "run_layer_calls",          # total run_layer invocations
#     "get_cluster_info_calls",
#     "total_tool_calls",         # all tool calls combined

#     # ── Pure compute timing ───────────────────────────────────────────────
#     "elapsed_seconds",          # sum of PARCS totalElapsedSeconds (or python_exec)
#     "wall_clock_seconds",       # total including all LLM turns

#     # ── Cold-start / scheduling overhead (parallel only) ─────────────────
#     # overhead = (completedAt - submittedAt) - totalElapsedSeconds per layer
#     # Layer 1 includes KEDA pod spin-up; later layers reuse warm pods.
#     "cold_start_first_layer_seconds",
#     "cold_start_subsequent_avg_seconds",
#     "cold_start_json",          # [overhead_L1, overhead_L2, ...] seconds

#     # ── Parallelism (parallel only) ───────────────────────────────────────
#     "parallelism_used",         # max parallelism across all run_layer calls
#     "total_worker_slots",       # sum of parallelism across all layers
#     "workers_succeeded",        # total workers with success=true
#     "workers_failed",           # total workers with success=false
#     "worker_success_rate",      # workers_succeeded / total_worker_slots
#     "min_worker_seconds",       # fastest individual worker
#     "max_worker_seconds",       # slowest individual worker
#     "load_imbalance_ratio",     # max_worker / min_worker (1.0 = perfect balance)
#     "worker_timings_json",      # [[w0, w1, ...], [...]] seconds, per layer

#     # ── PARCS session info ────────────────────────────────────────────────
#     "parcs_session_id",
#     "parcs_layer_ids_json",     # [layerId_L1, layerId_L2, ...]

#     # ── Result ───────────────────────────────────────────────────────────
#     "result_summary",           # agent's one-line description
#     "result_json",              # agent's structured JSON output block
#     "parcs_last_output_json",   # raw resultJson from the last run_layer call
#     "result_output_bytes",      # total bytes in all worker outputData fields

#     # ── Errors ───────────────────────────────────────────────────────────
#     "agent_error",
# ]


# # ── Regex helpers ──────────────────────────────────────────────────────────────

# _JSON_FENCE  = re.compile(r"```json\s*(\{.*?\})\s*```", re.DOTALL)
# _ELAPSED_RE  = re.compile(r"elapsed_seconds:\s*([\d.]+)")


# def _extract_result_json(text: str) -> dict[str, Any] | None:
#     matches = _JSON_FENCE.findall(text)
#     if not matches:
#         return None
#     try:
#         return json.loads(matches[-1])
#     except json.JSONDecodeError:
#         return None


# def _collect_final_text(messages: list) -> str:
#     from langchain_core.messages import AIMessage
#     for msg in reversed(messages):
#         if isinstance(msg, AIMessage):
#             content = msg.content
#             if isinstance(content, str):
#                 return content
#             if isinstance(content, list):
#                 return "\n".join(
#                     p.get("text", "") for p in content
#                     if isinstance(p, dict) and p.get("type") == "text"
#                 )
#     return ""


# def _msg_text(content: Any) -> str:
#     if isinstance(content, str):
#         return content
#     if isinstance(content, list):
#         return "\n".join(
#             p.get("text", "") if isinstance(p, dict) else str(p)
#             for p in content
#         )
#     return str(content)


# # ── Metrics extraction ─────────────────────────────────────────────────────────

# def extract_metrics(messages: list, mode: Mode) -> dict[str, Any]:
#     """Walk the full message list and extract every measurable metric."""
#     from langchain_core.messages import AIMessage, ToolMessage

#     m: dict[str, Any] = {
#         "llm_turns": 0,
#         "input_tokens": 0,
#         "output_tokens": 0,
#         "total_tokens": 0,
#         "create_session_attempts": 0,
#         "compilation_failures": 0,
#         "run_layer_calls": 0,
#         "get_cluster_info_calls": 0,
#         "total_tool_calls": 0,
#         "elapsed_seconds": 0.0,
#         "parallelism_used": 0,
#         "total_worker_slots": 0,
#         "workers_succeeded": 0,
#         "workers_failed": 0,
#         "worker_timings": [],           # list[list[float]], one per run_layer
#         "cold_start_overheads": [],     # list[float], one per run_layer
#         "min_worker_seconds": "",
#         "max_worker_seconds": "",
#         "load_imbalance_ratio": "",
#         "parcs_session_id": "",
#         "parcs_layer_ids": [],
#         "parcs_last_output_json": "",
#         "result_output_bytes": 0,
#     }

#     for msg in messages:
#         if isinstance(msg, AIMessage):
#             m["llm_turns"] += 1
#             # LangChain standard field
#             usage = getattr(msg, "usage_metadata", None) or {}
#             m["input_tokens"]  += usage.get("input_tokens", 0)
#             m["output_tokens"] += usage.get("output_tokens", 0)
#             m["total_tokens"]  += usage.get("total_tokens", 0)
#             # Gemini thinking models store counts in response_metadata
#             if not usage:
#                 rmeta = getattr(msg, "response_metadata", None) or {}
#                 usage_meta = rmeta.get("usage_metadata", {}) or {}
#                 m["input_tokens"]  += (
#                     usage_meta.get("prompt_token_count", 0)
#                     or usage_meta.get("input_tokens", 0)
#                 )
#                 output_toks = (
#                     usage_meta.get("candidates_token_count", 0)
#                     or usage_meta.get("output_tokens", 0)
#                 )
#                 thoughts_toks = usage_meta.get("thoughts_token_count", 0)
#                 m["output_tokens"] += output_toks + thoughts_toks
#                 m["total_tokens"]  += (
#                     usage_meta.get("total_token_count", 0)
#                     or m["input_tokens"] + m["output_tokens"]
#                 )

#         elif isinstance(msg, ToolMessage):
#             name    = (getattr(msg, "name", "") or "").lower()
#             content = _msg_text(msg.content)
#             m["total_tool_calls"] += 1

#             if "create_session" in name:
#                 m["create_session_attempts"] += 1
#                 low = content.lower()
#                 if "compilation failed" in low or '"error"' in low or "error:" in low:
#                     m["compilation_failures"] += 1
#                 else:
#                     # Extract session id from successful create_session response
#                     try:
#                         parsed = json.loads(content)
#                         sid = parsed.get("sessionId", "")
#                         if sid and not m["parcs_session_id"]:
#                             m["parcs_session_id"] = sid
#                     except Exception:
#                         pass

#             elif "run_layer" in name:
#                 m["run_layer_calls"] += 1
#                 _parse_run_layer(content, m)

#             elif "get_cluster_info" in name:
#                 m["get_cluster_info_calls"] += 1

#             # Sequential: accumulate elapsed from python_exec
#             if mode == "sequential" and "elapsed_seconds:" in content:
#                 match = _ELAPSED_RE.search(content)
#                 if match:
#                     m["elapsed_seconds"] += float(match.group(1))

#     # Aggregate worker stats
#     all_times = [t for layer in m["worker_timings"] for t in layer if t > 0]
#     if all_times:
#         mn = round(min(all_times), 3)
#         mx = round(max(all_times), 3)
#         m["min_worker_seconds"]   = mn
#         m["max_worker_seconds"]   = mx
#         m["load_imbalance_ratio"] = round(mx / mn, 3) if mn > 0 else ""

#     # Worker success rate
#     total_slots = m["total_worker_slots"]
#     if total_slots:
#         m["worker_success_rate"] = round(m["workers_succeeded"] / total_slots, 4)
#     else:
#         m["worker_success_rate"] = ""

#     # Cold-start summary
#     overheads = m.pop("cold_start_overheads")
#     m["cold_start_json"]                   = json.dumps([round(o, 3) for o in overheads])
#     m["cold_start_first_layer_seconds"]    = round(overheads[0], 3) if overheads else ""
#     m["cold_start_subsequent_avg_seconds"] = (
#         round(sum(overheads[1:]) / len(overheads[1:]), 3) if len(overheads) > 1 else ""
#     )

#     m["worker_timings_json"]  = json.dumps(m.pop("worker_timings"))
#     m["parcs_layer_ids_json"] = json.dumps(m.pop("parcs_layer_ids"))
#     m["elapsed_seconds"]      = round(m["elapsed_seconds"], 3)
#     return m


# def _parse_run_layer(content: str, m: dict) -> None:
#     """Extract all PARCS metrics from a single run_layer ToolMessage."""
#     try:
#         outer = json.loads(content)
#         result_json_str = outer.get("resultJson", "")
#         if not result_json_str:
#             return

#         m["parcs_last_output_json"] = result_json_str

#         # Collect layer IDs
#         lid = outer.get("layerId", "")
#         if lid:
#             m["parcs_layer_ids"].append(lid)

#         layer        = json.loads(result_json_str)
#         total_elapsed = float(layer.get("totalElapsedSeconds", 0))
#         m["elapsed_seconds"] += total_elapsed

#         # Cold-start = wall time from submission → completion  minus  pure compute
#         for ts_key in ("submittedAt", "SubmittedAt"):
#             for tc_key in ("completedAt", "CompletedAt"):
#                 submitted_str = outer.get(ts_key, "")
#                 completed_str = outer.get(tc_key, "")
#                 if submitted_str and completed_str:
#                     try:
#                         t0 = datetime.fromisoformat(submitted_str.rstrip("Z")).replace(tzinfo=timezone.utc)
#                         t1 = datetime.fromisoformat(completed_str.rstrip("Z")).replace(tzinfo=timezone.utc)
#                         overhead = max(0.0, (t1 - t0).total_seconds() - total_elapsed)
#                         m["cold_start_overheads"].append(overhead)
#                     except Exception:
#                         pass

#         # Per-worker breakdown
#         results = layer.get("results", [])
#         worker_times = []
#         for r in results:
#             elapsed = float(r.get("elapsedSeconds", 0))
#             worker_times.append(elapsed)
#             if r.get("success", False):
#                 m["workers_succeeded"] += 1
#             else:
#                 m["workers_failed"] += 1
#             # Accumulate output size
#             output = r.get("outputData", "") or ""
#             m["result_output_bytes"] += len(output.encode("utf-8"))

#         if worker_times:
#             m["worker_timings"].append(worker_times)
#             n = len(worker_times)
#             m["total_worker_slots"] += n
#             if n > m["parallelism_used"]:
#                 m["parallelism_used"] = n

#     except Exception:
#         pass


# # ── Single-task runner ─────────────────────────────────────────────────────────

# async def _stream_agent(
#     agent, prompt: str, system: str, display: AgentDisplay, console: Console
# ) -> list:
#     """Stream the agent executor and render messages as they arrive."""
#     messages: list = []
#     with Live(display.spinner, console=console, refresh_per_second=10, transient=True) as live:
#         # agent is now an async generator function: agent(prompt) -> AsyncGen[list]
#         async for all_msgs in agent(prompt):
#             new = all_msgs[display.printed_count:]
#             if not new:
#                 continue
#             messages = all_msgs
#             live.stop()
#             for msg in new:
#                 display.print_message(msg)
#             display.printed_count = len(messages)
#             live.update(display.spinner)
#             live.start()
#     return messages


# async def run_task(
#     task: BenchmarkTask,
#     model_name: str,
#     mode: Mode,
#     console: Console,
# ) -> dict[str, Any]:
#     run_id = f"task{task.task_id}_{mode}_{model_name.replace('/', '-')}_{int(time.time())}"
#     color  = "cyan" if mode == "parallel" else "yellow"

#     console.print(Panel(
#         f"[bold]Task {task.task_id}[/] {task.title}  "
#         f"mode=[{color}]{mode}[/]  model=[white]{model_name}[/]",
#         border_style=color,
#     ))

#     if mode == "parallel":
#         agent, system = await create_parcs_agent(model_name)
#         prompt = make_parallel_prompt(task)
#     else:
#         agent, system = create_sequential_agent(model_name)
#         prompt = make_sequential_prompt(task)

#     display     = AgentDisplay(console)
#     wall_start  = time.monotonic()
#     messages: list = []
#     agent_error = ""
#     status      = "completed"

#     try:
#         async with asyncio.timeout(config.eval.timeout_seconds):
#             messages = await _stream_agent(agent, prompt, system, display, console)
#     except TimeoutError:
#         agent_error = f"timeout after {config.eval.timeout_seconds}s"
#         status = "timeout"
#         console.print("[red]✗ Timed out[/]")
#     except Exception as exc:  # noqa: BLE001
#         agent_error = str(exc)
#         status = "failed"
#         console.print(f"[red]✗ Error: {exc}[/]")

#     wall_seconds = round(time.monotonic() - wall_start, 2)
#     metrics      = extract_metrics(messages, mode)
#     final_text   = _collect_final_text(messages)
#     parsed       = _extract_result_json(final_text) or {}

#     elapsed = metrics["elapsed_seconds"] or parsed.get("elapsed_seconds", "")

#     result: dict[str, Any] = {
#         # Task identity
#         "run_id":                  run_id,
#         "timestamp":               datetime.now(timezone.utc).isoformat(),
#         "model":                   model_name,
#         "task_id":                 task.task_id,
#         "task_title":              task.title,
#         "task_domain":             task.domain,
#         "task_question":           task.question,
#         "task_reference_answer":   task.answer,
#         "mode":                    mode,
#         "status":                  status,
#         # LLM
#         "llm_turns":               metrics["llm_turns"],
#         "input_tokens":            metrics["input_tokens"],
#         "output_tokens":           metrics["output_tokens"],
#         "total_tokens":            metrics["total_tokens"],
#         # Tools
#         "create_session_attempts": metrics["create_session_attempts"],
#         "compilation_failures":    metrics["compilation_failures"],
#         "run_layer_calls":         metrics["run_layer_calls"],
#         "get_cluster_info_calls":  metrics["get_cluster_info_calls"],
#         "total_tool_calls":        metrics["total_tool_calls"],
#         # Timing
#         "elapsed_seconds":         elapsed,
#         "wall_clock_seconds":      wall_seconds,
#         # Cold start
#         "cold_start_first_layer_seconds":    metrics["cold_start_first_layer_seconds"],
#         "cold_start_subsequent_avg_seconds": metrics["cold_start_subsequent_avg_seconds"],
#         "cold_start_json":                   metrics["cold_start_json"],
#         # Parallelism
#         "parallelism_used":        metrics["parallelism_used"] or parsed.get("parallelism_used", ""),
#         "total_worker_slots":      metrics["total_worker_slots"],
#         "workers_succeeded":       metrics["workers_succeeded"],
#         "workers_failed":          metrics["workers_failed"],
#         "worker_success_rate":     metrics["worker_success_rate"],
#         "min_worker_seconds":      metrics["min_worker_seconds"],
#         "max_worker_seconds":      metrics["max_worker_seconds"],
#         "load_imbalance_ratio":    metrics["load_imbalance_ratio"],
#         "worker_timings_json":     metrics["worker_timings_json"],
#         # PARCS session
#         "parcs_session_id":        metrics["parcs_session_id"],
#         "parcs_layer_ids_json":    metrics["parcs_layer_ids_json"],
#         # Result
#         "result_summary":          parsed.get("result_summary", ""),
#         "result_json":             json.dumps(parsed) if parsed else "",
#         "parcs_last_output_json":  metrics["parcs_last_output_json"],
#         "result_output_bytes":     metrics["result_output_bytes"],
#         # Errors
#         "agent_error":             agent_error,
#     }

#     _log_result(result, console)
#     return result


# def _log_result(r: dict[str, Any], console: Console) -> None:
#     if r["agent_error"]:
#         console.print(f"  [red]{r['status']}: {r['agent_error']}[/]")
#         return
#     # Warn when the agent answered without running any computation
#     if r["mode"] == "parallel" and not r.get("run_layer_calls"):
#         console.print(
#             "  [bold red]⚠ NO run_layer CALLS — agent answered from memory, "
#             "not from cluster. Result is unreliable.[/]"
#         )
#     if r["mode"] == "sequential" and not r.get("elapsed_seconds"):
#         console.print(
#             "  [bold red]⚠ NO python_exec calls — agent answered from memory, "
#             "not from computation. Result is unreliable.[/]"
#         )
#     color = "cyan" if r["mode"] == "parallel" else "yellow"
#     parts = [
#         f"[{color}]{r['mode']}[/]",
#         f"elapsed=[bold]{r['elapsed_seconds']}s[/]",
#     ]
#     if r.get("parallelism_used"):
#         parts.append(f"workers=[bold]{r['parallelism_used']}[/]")
#     if r.get("cold_start_first_layer_seconds"):
#         parts.append(f"cold_start=[dim]{r['cold_start_first_layer_seconds']}s[/]")
#     if r.get("load_imbalance_ratio"):
#         parts.append(f"imbalance=[dim]{r['load_imbalance_ratio']}×[/]")
#     if r.get("total_tokens"):
#         parts.append(f"tokens=[dim]{r['total_tokens']}[/]")
#     console.print("  " + "  ".join(parts))
#     if r.get("result_summary"):
#         console.print(f"  [dim]{r['result_summary']}[/]")


# # ── CSV helpers ────────────────────────────────────────────────────────────────

# def _ensure_csv(path: Path) -> None:
#     """Create the CSV with headers if it doesn't exist, or patch in missing headers."""
#     path.parent.mkdir(parents=True, exist_ok=True)

#     if not path.exists() or path.stat().st_size == 0:
#         with path.open("w", newline="", encoding="utf-8") as f:
#             csv.DictWriter(f, fieldnames=RESULT_FIELDS).writeheader()
#         return

#     # File exists — check that all expected columns are present.
#     with path.open(newline="", encoding="utf-8") as f:
#         existing = csv.DictReader(f)
#         existing_fields = existing.fieldnames or []

#     missing = [c for c in RESULT_FIELDS if c not in existing_fields]
#     if not missing:
#         return

#     # Schema changed: rewrite with updated headers, keeping existing data intact.
#     console_warn = f"[warn] CSV schema updated — adding {len(missing)} new column(s): {missing}"
#     print(console_warn)

#     with path.open(newline="", encoding="utf-8") as f:
#         rows = list(csv.DictReader(f))

#     with path.open("w", newline="", encoding="utf-8") as f:
#         writer = csv.DictWriter(f, fieldnames=RESULT_FIELDS, extrasaction="ignore")
#         writer.writeheader()
#         for row in rows:
#             writer.writerow(row)


# def append_result(result: dict[str, Any], path: Path) -> None:
#     _ensure_csv(path)
#     with path.open("a", newline="", encoding="utf-8") as f:
#         csv.DictWriter(f, fieldnames=RESULT_FIELDS, extrasaction="ignore").writerow(result)


# def load_completed(path: Path) -> set[tuple[int, str, str]]:
#     """Return (task_id, model, mode) triples that already have a successful result."""
#     if not path.exists():
#         return set()
#     completed: set[tuple[int, str, str]] = set()
#     with path.open(newline="", encoding="utf-8") as f:
#         for row in csv.DictReader(f):
#             if row.get("status") == "completed" and row.get("elapsed_seconds"):
#                 completed.add((int(row["task_id"]), row["model"], row["mode"]))
#     return completed


# # ── Main benchmark loop ────────────────────────────────────────────────────────

# async def run_benchmark(
#     tasks: list[BenchmarkTask],
#     models: list[str],
#     modes: list[Mode],
#     results_path: Path,
#     console: Console,
#     skip_completed: bool = True,
# ) -> None:
#     completed = load_completed(results_path) if skip_completed else set()
#     total = len(tasks) * len(models) * len(modes)
#     done  = 0

#     for model in models:
#         for mode in modes:
#             for task in tasks:
#                 done += 1
#                 if skip_completed and (task.task_id, model, mode) in completed:
#                     console.print(
#                         f"[dim]Skipping task {task.task_id} ({task.title}) "
#                         f"/ {mode} / {model}[/]"
#                     )
#                     continue
#                 console.print(
#                     f"\n[bold white]── [{done}/{total}] "
#                     f"Task {task.task_id} · {task.title} · {mode} · {model} ──[/]"
#                 )
#                 result = await run_task(task, model, mode, console)
#                 append_result(result, results_path)

#     console.print(f"\n[bold green]✓ All done. Results: {results_path}[/]")
