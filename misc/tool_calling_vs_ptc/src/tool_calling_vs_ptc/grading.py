"""Grading, pre-registered in the backlog (2.0): success = correct answer AND the prescribed layer plan.

The plan is checked by provenance on the *completed* layers as the server lists them: the layer whose
output is the reported answer (else the latest layer nothing reads), together with its ancestors
through ``previousLayerIds``, must be exactly the prescribed plan. Other layers, such as a crashed
attempt or a whole plan rerun after a bug fix, do not break it; they are counted (``layers_extra``,
``layers_other``) and show up in the cost metrics.
"""

from __future__ import annotations

from typing import Any

from .tasks import parse_answer, tolerance


def _numbers(value: Any):
    if isinstance(value, bool):
        return
    if isinstance(value, int | float):
        yield float(value)
    elif isinstance(value, str):
        try:
            yield float(value)
        except ValueError:
            return
    elif isinstance(value, dict):
        for v in value.values():
            yield from _numbers(v)
    elif isinstance(value, list):
        for v in value:
            yield from _numbers(v)


def layer_outputs(tool_calls: list[dict]) -> dict[str, Any]:
    """The last `output` seen for each completed layerId in the tool-call log."""
    outputs: dict[str, Any] = {}
    for call in tool_calls:
        env = call.get("envelope") or {}
        if env.get("layerId") and env.get("status") == "Completed" and env.get("output") is not None:
            outputs[env["layerId"]] = env["output"]
    return outputs


def _prev(layer: dict) -> list[str]:
    return list(layer.get("previousLayerIds") or [])


def _matches(output: Any, answer: float, tol: float) -> bool:
    return any(abs(x - answer) <= tol for x in _numbers(output))


def final_layer(completed: list[dict], outputs: dict[str, Any], answer: float | None, tol: float) -> dict | None:
    """The latest completed layer that output the answer (one that reads parents first), else the latest
    layer no other completed layer reads. ``completed`` is in submission order."""
    if answer is not None:
        hits = [i for i, layer in enumerate(completed) if _matches(outputs.get(layer["layerId"]), answer, tol)]
        if hits:
            return completed[max(hits, key=lambda i: (bool(_prev(completed[i])), i))]
    parents = {p for layer in completed for p in _prev(layer)}
    leaves = [layer for layer in completed if layer["layerId"] not in parents]
    return leaves[-1] if leaves else None


def check_f1(final: dict, by_id: dict[str, dict], n: int) -> tuple[list[str], set[str]]:
    prev = _prev(final)
    subs = [by_id[p] for p in prev if p in by_id]
    reasons = []
    if final.get("parallelism") != 1:
        reasons.append("the reduce layer has parallelism != 1")
    if len(prev) != n or len(set(prev)) != n or len(subs) != n:
        reasons.append(f"the reduce layer reads {len(subs)} completed layers, expected {n} subinterval layers")
    if any(layer.get("parallelism") != 1 or _prev(layer) for layer in subs):
        reasons.append("a subinterval layer has parallelism != 1 or reads other layers")
    return reasons, {final["layerId"], *(s["layerId"] for s in subs)}


def check_f2(final: dict, by_id: dict[str, dict], n: int, max_parallelism: int) -> tuple[list[str], set[str]]:
    chain, layer = [final], final
    while _prev(layer):
        prev = _prev(layer)
        if len(prev) != 1 or prev[0] not in by_id:
            return [f"layer {layer['layerId']} reads {len(prev)} layers, expected 1 completed step"], {
                x["layerId"] for x in chain
            }
        layer = by_id[prev[0]]
        chain.append(layer)
    steps = chain[1:]
    reasons = []
    if len(steps) != n:
        reasons.append(f"the final layer sits on {len(steps)} step layers, expected {n}")
    if any(step.get("parallelism") != max_parallelism for step in steps):
        reasons.append(f"a step layer has parallelism != maxParallelism ({max_parallelism})")
    if final.get("parallelism") != 1:
        reasons.append("the final layer has parallelism != 1")
    return reasons, {x["layerId"] for x in chain}


def grade(
    family: str, n: int, final_text: str, layers: list[dict], tool_calls: list[dict], truth: float, max_parallelism: int
) -> dict:
    answer = parse_answer(final_text)
    tol = tolerance(family, n)
    abs_error = None if answer is None else abs(answer - truth)
    correct = abs_error is not None and abs_error <= tol

    completed = [layer for layer in layers if layer.get("status") == "Completed"]
    by_id = {layer["layerId"]: layer for layer in completed}
    outputs = layer_outputs(tool_calls)
    final = final_layer(completed, outputs, answer, tol)
    if final is None:
        reasons, plan = ["no completed layer"], set()
    elif family == "f1":
        reasons, plan = check_f1(final, by_id, n)
    else:
        reasons, plan = check_f2(final, by_id, n, max_parallelism)

    final_output = outputs.get(final["layerId"]) if final else None
    if answer is not None and final is not None and not _matches(final_output, answer, tol):
        reasons.append("the reported answer is not the output of a plan's final layer")

    return {
        "answer": answer,
        "truth": truth,
        "tolerance": tol,
        "abs_error": abs_error,
        "correct": correct,
        "protocol_ok": not reasons,
        "protocol_reasons": reasons,
        "layers_completed": len(completed),
        "layers_extra": len(completed) - len(plan),
        "layers_other": len(layers) - len(completed),
        "final_output": final_output,
        "plan_layer_ids": sorted(plan),
        "success": correct and not reasons,
    }
