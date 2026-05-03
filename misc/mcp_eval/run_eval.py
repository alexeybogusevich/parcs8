# Silence LangSmith before anything else imports langchain/langsmith
import os, warnings
os.environ["LANGCHAIN_TRACING_V2"] = "false"
os.environ.pop("LANGCHAIN_API_KEY", None)
os.environ.pop("LANGSMITH_API_KEY", None)
warnings.filterwarnings("ignore", category=UserWarning, module="langsmith")

"""Entry point for the benchmark evaluation run.

Usage:
    # Full benchmark (sequential + parallel, both models)
    python run_eval.py

    # Only parallel runs
    python run_eval.py --modes parallel

    # Single task, single model, both modes
    python run_eval.py --tasks 1 --models gemini-2.0-flash-001

    # Rerun even if already in results file
    python run_eval.py --no-skip

    # Dry-run: print the plan without running anything
    python run_eval.py --dry-run
"""

import asyncio
from argparse import ArgumentParser
from pathlib import Path

from rich.console import Console
from rich.table import Table

from mcp_eval.config import config
from mcp_eval.evaluator import load_completed, run_benchmark
from mcp_eval.tasks import load_tasks


def parse_args():
    p = ArgumentParser(description="PARCS-Agent benchmark evaluation")
    p.add_argument("--tasks",   default="", help="Comma-separated task IDs (default: all 15)")
    p.add_argument("--models",  default="", help="Comma-separated model names (overrides config)")
    p.add_argument("--modes",   default="sequential,parallel",
                   help="Modes to run: sequential, parallel, or both (default: both)")
    p.add_argument("--results", default="", help="Path to results CSV (overrides config)")
    p.add_argument("--no-skip", action="store_true", help="Re-run already completed tasks")
    p.add_argument("--dry-run", action="store_true", help="Print plan without executing")
    return p.parse_args()


async def main() -> None:
    args = parse_args()
    console = Console()

    models = [m.strip() for m in (args.models or config.eval.models).split(",") if m.strip()]
    modes  = [m.strip() for m in args.modes.split(",") if m.strip()]

    raw_ids = args.tasks or config.eval.task_ids
    task_ids = [int(x.strip()) for x in raw_ids.split(",") if x.strip()] if raw_ids else None

    console.print("[bold]Loading benchmark tasks from HuggingFace…[/]")
    tasks = load_tasks(task_ids)
    console.print(f"  {len(tasks)} task(s) loaded\n")

    results_path = Path(args.results) if args.results else config.eval.results_file
    already_done = load_completed(results_path)
    skip = not args.no_skip

    table = Table(title="Planned runs", show_lines=True)
    table.add_column("Task", justify="center")
    table.add_column("Mode")
    table.add_column("Model")
    table.add_column("Status")

    planned = 0
    for model in models:
        for mode in modes:
            for task in tasks:
                if skip and (task.task_id, model, mode) in already_done:
                    status = "[dim]skip (done)[/]"
                else:
                    status = "[green]will run[/]"
                    planned += 1
                table.add_row(str(task.task_id), mode, model, status)

    console.print(table)
    console.print(
        f"\n[bold]{planned}[/] run(s) planned  |  results → [cyan]{results_path}[/]\n"
    )

    if args.dry_run:
        console.print("[yellow]--dry-run: exiting without running[/]")
        return

    if planned == 0:
        console.print("[green]Nothing to do — all tasks already completed.[/]")
        return

    await run_benchmark(
        tasks=tasks,
        models=models,
        modes=modes,
        results_path=results_path,
        console=console,
        skip_completed=skip,
    )


if __name__ == "__main__":
    asyncio.run(main())
