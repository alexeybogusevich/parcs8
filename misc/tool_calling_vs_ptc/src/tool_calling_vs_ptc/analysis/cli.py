"""tcvp-metrics RESULTS_DIR | tcvp-metrics --exploratory

RESULTS_DIR: regrade every run and write ``metrics.jsonl`` and ``metrics.csv`` next to ``runs.jsonl``,
with costs from the directory's ``pricing.json`` snapshot (fetched on first use).
--exploratory: fetch the six traces of the exploratory run from LangSmith into
``tests/fixtures/exploratory`` and print their metrics next to the published table (backlog 4.3).
--crosscheck RESULTS_DIR: load every run both from its transcript and from its LangSmith trace and
report any metric the two disagree on.
"""

from __future__ import annotations

import argparse
import csv
import json
import statistics
from collections import defaultdict
from pathlib import Path

from ..plan import load_records
from .metrics import run_metrics
from .normalize import from_langsmith, from_transcript
from .pricing import snapshot

ROOT = Path(__file__).resolve().parents[3]
FIXTURES = ROOT / "tests" / "fixtures" / "exploratory"
EXPLORATORY_PROJECT = "parcs-agent-benchmark"
# The published table: LLM calls, prompt tokens, completion tokens, cost (USD), wall (s), layers, evals.
EXPLORATORY = {
    "pi-tool_calling": ("01a17317-d5d0-7d31-84e9-d62089557d8b", (16, 119145, 4204, 0.0094, 106, 14, None)),
    "pi-ptc": ("01a17319-8a8d-7970-a331-d1869d44a482", (2, 11726, 2052, 0.0039, 68, 14, 1)),
    "bisection-tool_calling": ("01a1731a-ab16-7e72-a5d1-96ef4c2f4f35", (21, 176999, 4436, 0.0110, 136, 17, None)),
    "bisection-ptc": ("01a1731c-d51c-7ac2-b245-3b0c0ba4ef92", (4, 26397, 3796, 0.0065, 102, 16, 3)),
    "pipeline-tool_calling": ("01a1731e-79ba-77d2-8203-7118be9b9520", (8, 58245, 4412, 0.0081, 68, 4, None)),
    "pipeline-ptc": ("01a1731f-9856-70f1-b4e2-ccd00a6414d6", (2, 11886, 2436, 0.0044, 35, 3, 1)),
}
EXPLORATORY_COLUMNS = ("llm_calls", "input_tokens", "output_tokens", "cost_usd", "wall_s", "layers_started", "evals")
# What the client log and the trace must agree on.
CROSSCHECK = ("llm_calls", "input_tokens", "output_tokens", "cached_input_tokens", "reasoning_tokens", "parcs_calls",
              "layers_started", "evals", "eval_errors", "max_layer_batch", "plan_turns")  # fmt: skip
SUMMARY = ("llm_calls", "input_tokens", "output_tokens", "cost_usd", "wall_s", "plan_turns", "success")


def extract(results: Path, refresh_pricing: bool = False) -> list[dict]:
    records = load_records(results / "runs.jsonl")
    transcripts = {p.stem: p for p in sorted((results / "transcripts").glob("*.json"))}
    runs = [
        from_transcript(json.loads(p.read_text(encoding="utf-8")), records.get(rid)) for rid, p in transcripts.items()
    ]
    prices = snapshot(results / "pricing.json", sorted({r.model_name for r in runs}), refresh_pricing, pinned(results))
    rows = [run_metrics(r, prices) for r in runs]
    with (results / "metrics.jsonl").open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row) + "\n")
    if rows:
        with (results / "metrics.csv").open("w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
    return rows


def summarize(rows: list[dict]) -> None:
    cells = defaultdict(list)
    for row in rows:
        cells[(row["family"], row["n"], row["model"], row["arm"])].append(row)
    print("family n model arm reps", *(f"median_{k}" for k in SUMMARY), sep="\t")
    for key in sorted(cells, key=lambda k: (k[0] or "", k[1] or 0, k[2] or "", k[3])):
        group = cells[key]
        medians = []
        for k in SUMMARY:
            values = [float(r[k]) for r in group if r[k] is not None]
            medians.append(f"{statistics.median(values):.4g}" if values else "-")
        print(*key, len(group), *medians, sep="\t")


def _plain(run) -> dict:
    """The fields ``from_langsmith`` reads, without prompts (LLM inputs) or C# sources."""
    out = {k: getattr(run, k) for k in ("id", "parent_run_id", "parent_run_ids", "run_type", "name", "start_time",
                                         "end_time", "total_cost", "error")}  # fmt: skip
    out = {k: (str(v) if v is not None and (k.endswith("id") or k.endswith("time")) else v) for k, v in out.items()}
    out["parent_run_ids"] = [str(p) for p in run.parent_run_ids or []]
    out["total_cost"] = None if run.total_cost is None else float(run.total_cost)  # a Decimal in the SDK
    if not run.parent_run_id:
        out["extra"] = {"metadata": (run.extra or {}).get("metadata") or {}}
    if run.run_type == "llm":
        message = ((run.outputs or {}).get("generations") or [[{}]])[0][0].get("message") or {}
        kw = message.get("kwargs") or {}
        keep = {k: kw.get(k) for k in ("content", "tool_calls", "usage_metadata")}
        keep["tool_calls"] = [{"name": c["name"]} for c in keep["tool_calls"] or []]
        out["outputs"] = {"generations": [[{"message": {"kwargs": keep}}]]}
    elif run.run_type == "tool":
        inputs = dict(run.inputs or {})
        if isinstance(inputs.get("sourceCode"), str):
            inputs["sourceCode"] = f"<{len(inputs['sourceCode'])} chars>"
        out["inputs"] = {k: v for k, v in inputs.items() if k != "code"}
        out["outputs"] = run.outputs
    return out


def fetch_trace(project: str, trace_id: str) -> list[dict]:
    from langsmith import Client

    from ..config import load_config

    load_config()
    return [_plain(r) for r in Client().list_runs(project_name=project, trace_id=trace_id)]


def fetch_exploratory() -> None:
    FIXTURES.mkdir(parents=True, exist_ok=True)
    for label, (trace_id, _) in EXPLORATORY.items():
        runs = fetch_trace(EXPLORATORY_PROJECT, trace_id)
        (FIXTURES / f"{label}.json").write_text(json.dumps(runs, default=str, indent=1), encoding="utf-8")


def pinned(results: Path) -> dict[str, str | None]:
    """Model name -> the OpenRouter endpoint it was pinned to, from the directory's manifest."""
    manifest = results / "manifest.json"
    if not manifest.exists():
        return {}
    models = json.loads(manifest.read_text(encoding="utf-8"))["manifest"].get("models") or []
    return {m["model_name"]: m.get("openrouter_provider") for m in models}


def crosscheck(transcript: dict, trace: list[dict], prices: dict) -> dict[str, tuple]:
    """The metrics on which the transcript and the trace of one run disagree, as (transcript, trace)."""
    ours = run_metrics(from_transcript(transcript), prices)
    theirs = run_metrics(from_langsmith(trace, ours["run_id"]), prices)
    return {k: (ours[k], theirs[k]) for k in CROSSCHECK if ours[k] != theirs[k]}


def crosscheck_dir(results: Path) -> None:
    from ..config import load_config

    project = load_config().langsmith.project
    prices = snapshot(results / "pricing.json", [])
    for path in sorted((results / "transcripts").glob("*.json")):
        transcript = json.loads(path.read_text(encoding="utf-8"))
        diff = crosscheck(transcript, fetch_trace(project, transcript["trace_id"]), prices)
        print(path.stem, diff or "agree")


def exploratory_rows() -> dict[str, dict]:
    prices = json.loads((FIXTURES / "pricing.json").read_text(encoding="utf-8"))
    rows = {}
    for label in EXPLORATORY:
        runs = json.loads((FIXTURES / f"{label}.json").read_text(encoding="utf-8"))
        rows[label] = run_metrics(from_langsmith(runs, label), prices)
    return rows


def main(argv=None) -> None:
    ap = argparse.ArgumentParser(prog="tcvp-metrics", description=__doc__)
    ap.add_argument("results", type=Path, nargs="?")
    ap.add_argument("--exploratory", action="store_true")
    ap.add_argument("--crosscheck", action="store_true", help="compare transcripts with their LangSmith traces")
    ap.add_argument("--refresh-pricing", action="store_true")
    args = ap.parse_args(argv)
    if args.exploratory:
        fetch_exploratory()
        snapshot(FIXTURES / "pricing.json", ["openai/gpt-5.6-luna"], args.refresh_pricing)
        print("run", *EXPLORATORY_COLUMNS, "langsmith_cost_usd", sep="\t")
        for label, row in exploratory_rows().items():
            expected = EXPLORATORY[label][1]
            cells = [f"{row[k] if not isinstance(row[k], float) else round(row[k], 4)} ({e})"
                     for k, e in zip(EXPLORATORY_COLUMNS, expected, strict=True)]  # fmt: skip
            print(label, *cells, round(row["langsmith_cost_usd"] or 0, 4), sep="\t")
    elif args.results and args.crosscheck:
        crosscheck_dir(args.results.resolve())
    elif args.results:
        summarize(extract(args.results.resolve(), args.refresh_pricing))
    else:
        ap.error("give a results directory or --exploratory")


if __name__ == "__main__":
    main()
