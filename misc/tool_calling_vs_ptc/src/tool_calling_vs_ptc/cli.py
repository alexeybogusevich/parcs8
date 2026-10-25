"""tcvp plan | run | status. Options override the EXPERIMENT__* settings; each results directory keeps
its own manifest, so a pilot and the sweep go to different directories."""

from __future__ import annotations

import argparse
import asyncio
import collections
import json
from pathlib import Path

from .cluster import Cluster
from .config import Config, load_config
from .manifest import build, ensure
from .plan import append_record, load_records, ordered_cells, pending
from .runner import execute

# A row of infrastructure failures means something is down; stop instead of churning through cells.
MAX_CONSECUTIVE_INFRA_ERRORS = 3
SUMMARY = ("run_id", "termination", "success", "correct", "protocol_ok", "llm_calls", "input_tokens", "wall_s")


def parse_args(argv=None):
    ap = argparse.ArgumentParser(prog="tcvp", description=__doc__)
    ap.add_argument("command", choices=["plan", "run", "status"])
    ap.add_argument("--results", type=Path, help="results directory (default EXPERIMENT__RESULTS_DIR)")
    ap.add_argument("--families", nargs="+", choices=["f1", "f2"])
    ap.add_argument("--n", type=int, nargs="+", dest="n_grid")
    ap.add_argument("--arms", nargs="+", choices=["tool_calling", "ptc"])
    ap.add_argument("--models", nargs="+", help="model ids from EXPERIMENT__MODELS")
    ap.add_argument("--reps", type=int)
    ap.add_argument("--limit", type=int, help="run at most this many pending cells")
    return ap.parse_args(argv)


def configure(args) -> Config:
    config = load_config()
    exp = config.experiment
    for name in ("families", "n_grid", "arms", "reps"):
        if getattr(args, name) is not None:
            setattr(exp, name, getattr(args, name))
    if args.results is not None:
        exp.results_dir = args.results.resolve()
    if args.models:
        known = {m.id: m for m in config.models()}
        missing = [m for m in args.models if m not in known]
        if missing:
            raise SystemExit(f"unknown model ids {missing}; configured: {sorted(known)}")
        exp.models = [known[m] for m in args.models]
    return config


def cells(config: Config):
    exp = config.experiment
    return ordered_cells(exp.families, exp.n_grid, exp.arms, [m.id for m in config.models()], exp.reps, exp.seed)


def status(config: Config) -> None:
    records = load_records(config.experiment.results_dir / "runs.jsonl")
    all_cells = cells(config)
    print(f"{len(all_cells)} cells, {len(records)} recorded, {len(pending(all_cells, records))} pending")
    print("(grades as recorded at run time; `tcvp-metrics` regrades from the transcripts)")
    by = collections.Counter(
        (r["family"], r["n"], r["arm"], r["model"], r["termination"], r["success"]) for r in records.values()
    )
    for key, count in sorted(by.items()):
        print(*key, count, sep="\t")


async def run(config: Config, limit: int | None) -> None:
    exp = config.experiment
    cluster = Cluster(config.mcp.cluster_url)
    manifest = build(config, await cluster.info())
    digest = ensure(exp.results_dir / "manifest.json", manifest)
    runs = exp.results_dir / "runs.jsonl"
    todo = pending(cells(config), load_records(runs))[:limit]
    specs = {m.id: m for m in config.models()}
    print(f"manifest {digest}: {len(todo)} runs to go -> {exp.results_dir}")
    infra_streak = 0
    for i, cell in enumerate(todo, 1):
        record = await execute(cell, config, specs[cell.model], cluster, manifest, digest, exp.results_dir)
        append_record(runs, record)
        infra_streak = infra_streak + 1 if record["termination"] == "infra_error" else 0
        line = {"i": f"{i}/{len(todo)}", **{k: record[k] for k in SUMMARY}}
        print(json.dumps(line, default=str))
        if infra_streak >= MAX_CONSECUTIVE_INFRA_ERRORS:
            raise SystemExit(f"{infra_streak} infra errors in a row (last: {record['error']}); stopping the sweep")


def main(argv=None) -> None:
    args = parse_args(argv)
    config = configure(args)
    if args.command == "plan":
        for cell in cells(config):
            print(cell.run_id)
    elif args.command == "status":
        status(config)
    else:
        asyncio.run(run(config, args.limit))


if __name__ == "__main__":
    main()
