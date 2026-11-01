"""Per-run metrics (backlog 4.1 and 4.2), one flat row per run, from a normalized ``Run``.

Runs are regraded here from what they recorded, so a later grading fix reaches every collected run.
"""

from __future__ import annotations

import bisect
import collections
import json
from pathlib import Path

from ..grading import grade
from .normalize import LAYER_STARTS, TERMINAL, Run
from .pricing import call_cost

TRUTH_FILE = Path(__file__).resolve().parents[3] / "tasks" / "ground_truth.json"
FAILED = ("CompletedWithFailures", "Failed", "Cancelled")


def _truth(family: str, n: int) -> float:
    return json.loads(TRUTH_FILE.read_text(encoding="utf-8"))[family]["per_n"][str(n)]["answer"]


def _union(intervals: list[tuple[float, float]]) -> float:
    total, end = 0.0, float("-inf")
    for a, b in sorted(intervals):
        if b > end:
            total += b - max(a, end)
            end = b
    return total


def _sum(values) -> float | None:
    values = list(values)
    return None if any(v is None for v in values) else sum(values)


def turns(run: Run) -> list[int | None]:
    """For each PARCS call, the index of the model call whose tool calls made it."""
    ends = sorted(c.t_end for c in run.calls if c.t_end is not None)
    if len(ends) != len(run.calls):
        return [None] * len(run.parcs)
    return [None if p.t_start is None else bisect.bisect_right(ends, p.t_start) - 1 for p in run.parcs]


def grade_run(run: Run, rows: list[dict]) -> dict | None:
    if run.family is None or run.n is None:
        return None
    calls = [{"envelope": p.envelope} for p in run.parcs]
    return grade(run.family, run.n, run.final_text, rows, calls, _truth(run.family, run.n), run.max_parallelism or 0)


def run_metrics(run: Run, prices: dict) -> dict:
    pricing = prices.get("models", {}).get(run.model_name)
    rows = run.layer_rows()
    own = run.own_sessions()
    g = grade_run(run, rows)

    # LLM usage and cost
    usage = [c.usage for c in run.calls]
    details = [u.get("input_token_details") or {} for u in usage]
    inp = sum(u.get("input_tokens") or 0 for u in usage)
    read = sum(d.get("cache_read") or 0 for d in details)
    write = sum(d.get("cache_creation") or 0 for d in details)
    out = sum(u.get("output_tokens") or 0 for u in usage)
    reasoning = sum((u.get("output_token_details") or {}).get("reasoning") or 0 for u in usage)

    # Time: model calls and PARCS calls never overlap each other; parallel PARCS calls overlap among themselves.
    model_s = sum(c.t_end - c.t_start for c in run.calls if c.t_start is not None and c.t_end is not None)
    parcs_s = _union([(p.t_start, p.t_end) for p in run.parcs if p.t_start is not None and p.t_end is not None])

    # Layers
    started = {p.envelope.get("layerId") for p in run.parcs if p.name in LAYER_STARTS and p.envelope.get("layerId")}
    status = collections.Counter(r.get("status") for r in rows)
    last_layer: dict[str, dict] = {}
    for p in run.parcs:
        if p.envelope.get("layerId") and p.envelope.get("layer") and p.envelope.get("sessionId") in own:
            last_layer[p.envelope["layerId"]] = p.envelope["layer"]
    errors = collections.Counter((p.envelope.get("error") or {}).get("code") for p in run.parcs
                                 if p.envelope.get("ok") is False)  # fmt: skip

    # Structure (4.2): batching per model turn, and how many turns the answer's plan took to issue
    per_message = [len(c.tool_calls) for c in run.calls if c.tool_calls]
    layer_batches = [sum(1 for name in c.tool_calls if name in LAYER_STARTS) for c in run.calls]
    plan_ids = set(g["plan_layer_ids"]) if g else {r["layerId"] for r in rows if r.get("status") == "Completed"}
    call_turns = turns(run)
    plan_turns = {t for p, t in zip(run.parcs, call_turns, strict=True)
                  if p.name in LAYER_STARTS and p.envelope.get("layerId") in plan_ids}  # fmt: skip
    evals = [r for r in run.tool_results if r.name == "eval"]
    eval_errors = collections.Counter(r.error_type for r in evals if r.error_type)

    return {
        "run_id": run.run_id,
        "family": run.family,
        "n": run.n,
        "arm": run.arm,
        "model": run.model or run.model_name,
        "rep": run.rep,
        "termination": run.termination,
        "success": g["success"] if g else None,
        "correct": g["correct"] if g else None,
        "protocol_ok": g["protocol_ok"] if g else None,
        "abs_error": g["abs_error"] if g else None,
        "protocol_reasons": "; ".join(g["protocol_reasons"]) if g else None,
        # 4.1 LLM
        "llm_calls": len(run.calls),
        "input_tokens": inp,
        "cached_input_tokens": read,
        "cache_write_tokens": write,
        "uncached_input_tokens": inp - read,
        "output_tokens": out,
        "reasoning_tokens": reasoning,
        "max_input_tokens": max((u.get("input_tokens") or 0 for u in usage), default=0),
        # Must be 0: the run nonce keeps runs from sharing a provider prompt cache.
        "first_call_cache_read": (details[0].get("cache_read") or 0) if details else None,
        "cost_usd": _sum(call_cost(u, pricing) for u in usage),
        "cost_no_cache_usd": _sum(call_cost(u, pricing, cached=False) for u in usage),
        "langsmith_cost_usd": _sum(c.langsmith_cost for c in run.calls) if run.calls else None,
        # 4.1 time
        "wall_s": run.wall_s,
        "model_s": model_s,
        "parcs_s": parcs_s,
        "other_s": None if run.wall_s is None else max(run.wall_s - model_s - parcs_s, 0.0),
        "queued_s": sum(layer.get("queuedSeconds") or 0 for layer in last_layer.values()),
        "layer_elapsed_s": sum(layer.get("elapsedSeconds") or 0 for layer in last_layer.values()),
        # 4.1 PARCS
        "parcs_calls": len(run.parcs),
        "layers_started": len(started),
        "layers_completed": status.get("Completed", 0),
        "layers_failed": sum(status.get(s, 0) for s in FAILED),
        "layers_unfinished": sum(c for s, c in status.items() if s not in TERMINAL),
        "layers_extra": g["layers_extra"] if g else None,
        "compile_errors": errors.get("compile_error", 0),
        "parcs_errors": sum(errors.values()),
        "parcs_error_codes": json.dumps(dict(errors)) if errors else "",
        "transport_errors": sum(1 for p in run.parcs if p.exception),
        "list_sessions_calls": sum(1 for p in run.parcs if p.name == "list_sessions"),
        "foreign_calls": sum(1 for p in run.parcs if p.foreign(own)),
        # 4.2 structure
        "tool_calls_per_message": sum(per_message) / len(per_message) if per_message else 0.0,
        "max_tool_batch": max(per_message, default=0),
        "max_layer_batch": max(layer_batches, default=0),
        "fanout_batched": None if run.n is None else max(layer_batches, default=0) >= run.n,
        "plan_layers": len(plan_ids),
        "plan_turns": None if None in plan_turns else len(plan_turns),
        "plan_in_one_turn": None if None in plan_turns else len(plan_turns) == 1,
        "evals": len(evals),
        "eval_errors": sum(eval_errors.values()),
        "eval_syntax_errors": eval_errors.get("SyntaxError", 0),
        "eval_error_types": json.dumps(dict(eval_errors)) if eval_errors else "",
        "tool_errors": sum(1 for r in run.tool_results if r.error_type),
    }
