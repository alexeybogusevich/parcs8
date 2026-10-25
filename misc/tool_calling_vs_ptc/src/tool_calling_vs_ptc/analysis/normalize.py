"""One run in a source-independent form, loaded from a runner transcript or from a LangSmith trace.

Both arms look the same here: the model calls (with usage and times), the results of the tools the
model called (``eval`` in ptc, PARCS tools in tool calling), and every PARCS call wherever it was
made (directly, or from inside ``eval``).
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any

PARCS_TOOLS = frozenset({
    "get_cluster_info", "create_session", "run_layer", "submit_layer", "wait_layer", "get_layer_result",
    "list_sessions", "list_layers", "cancel_layer",
})  # fmt: skip
LAYER_STARTS = frozenset({"run_layer", "submit_layer"})
TERMINAL = ("Completed", "CompletedWithFailures", "Failed", "Cancelled")
_EVAL_ERROR = re.compile(r'<error type="([^"]+)"')
# QuickJS puts <stdout> before <error>, and does not escape what the program printed.
_STDOUT = re.compile(r"<stdout>.*?</stdout>", re.DOTALL)


@dataclass
class ModelCall:
    usage: dict
    tool_calls: list[str]
    t_start: float | None = None
    t_end: float | None = None
    text: str = ""
    langsmith_cost: float | None = None


@dataclass
class ToolResult:
    """What the model got back from one of its own tool calls."""

    name: str
    status: str
    text: str

    @property
    def error_type(self) -> str | None:
        m = _EVAL_ERROR.search(_STDOUT.sub("", self.text))
        if m:
            return m.group(1)
        return "error" if self.status == "error" else None


@dataclass
class ParcsCall:
    name: str
    args: dict
    envelope: dict
    t_start: float | None
    t_end: float | None
    exception: str | None = None
    # Refused by the runner's private view: it named another run's session or layer.
    refused_foreign: bool = False

    def foreign(self, own: set[str]) -> bool:
        """Named, or was answered with, a session this run did not create."""
        sessions = {self.args.get("sessionId"), self.envelope.get("sessionId")} - {None}
        return self.refused_foreign or bool(sessions - own)


@dataclass
class Run:
    run_id: str
    arm: str
    model_name: str
    family: str | None = None
    n: int | None = None
    model: str | None = None
    rep: int | None = None
    wall_s: float | None = None
    termination: str | None = None
    max_parallelism: int | None = None
    calls: list[ModelCall] = field(default_factory=list)
    tool_results: list[ToolResult] = field(default_factory=list)
    parcs: list[ParcsCall] = field(default_factory=list)
    # The server's layer list; None when only the client's view exists (a LangSmith trace).
    layers: list[dict] | None = None
    final_text: str = ""

    def own_sessions(self) -> set[str]:
        """Sessions the run created. Only their layers count: anything else is another run's work."""
        return {p.envelope["sessionId"] for p in self.parcs
                if p.name == "create_session" and p.envelope.get("ok") and p.envelope.get("sessionId")}  # fmt: skip

    def layer_rows(self) -> list[dict]:
        """The layers of the run's own sessions: server rows if known, else one row per layerId from the
        envelopes (their last state)."""
        own = self.own_sessions()
        if self.layers is not None:
            return [row for row in self.layers if "sessionId" not in row or row["sessionId"] in own]
        rows: dict[str, dict] = {}
        for call in self.parcs:
            env = call.envelope or {}
            layer_id = env.get("layerId")
            if not layer_id or call.name not in LAYER_STARTS | {"wait_layer", "get_layer_result", "cancel_layer"}:
                continue
            if env.get("sessionId") not in own:
                continue
            layer = env.get("layer") or {}
            row = rows.setdefault(layer_id, {"layerId": layer_id})
            row.update({k: layer[k] for k in ("entryPoint", "parallelism", "previousLayerIds") if k in layer})
            if env.get("status"):
                row["status"] = env["status"]
        return list(rows.values())


def _text(content: Any) -> str:
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "".join(b.get("text", "") for b in content if isinstance(b, dict))
    return ""


def _json(text: str) -> dict:
    try:
        value = json.loads(text)
    except ValueError:
        return {}
    return value if isinstance(value, dict) else {}


def from_transcript(t: dict, record: dict | None = None) -> Run:
    """A run from ``transcripts/<run_id>.json``; ``record`` is its line in ``runs.jsonl`` (wall time)."""
    cell = t["cell"]
    meta = (t.get("config") or {}).get("metadata") or {}
    ais = [m["data"] for m in t["messages"] if m["type"] == "ai"]
    times = t.get("model_calls") or []
    calls = []
    for i, data in enumerate(ais):
        timing = times[i] if i < len(times) else {}
        calls.append(ModelCall(
            usage=data.get("usage_metadata") or {}, tool_calls=[c["name"] for c in data.get("tool_calls") or []],
            t_start=timing.get("t_start"), t_end=timing.get("t_end"), text=_text(data.get("content")),
        ))  # fmt: skip
    tool_messages = [m["data"] for m in t["messages"] if m["type"] == "tool"]
    results = [
        ToolResult(d.get("name") or "", d.get("status") or "success", _text(d["content"])) for d in tool_messages
    ]
    parcs = [ParcsCall(c["name"], c.get("args") or {}, c.get("envelope") or {}, c.get("t_start"), c.get("t_end"),
                       c.get("exception"), bool(c.get("foreign"))) for c in t.get("tool_calls") or []]  # fmt: skip
    final = next((c.text for c in reversed(calls) if not c.tool_calls), "") if t.get("termination") == "answer" else ""
    return Run(
        run_id=cell["run_id"], arm=cell["arm"], model_name=meta.get("model_name") or cell["model"],
        family=cell["family"], n=cell["n"], model=cell["model"], rep=cell["rep"],
        wall_s=(record or {}).get("wall_s"), termination=t.get("termination"),
        max_parallelism=(t.get("cluster_info") or {}).get("maxParallelism"),
        calls=calls, tool_results=results, parcs=parcs, layers=t.get("layers"), final_text=final,
    )  # fmt: skip


def _ts(value: Any) -> float | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value.timestamp()
    return datetime.fromisoformat(str(value).replace("Z", "+00:00")).timestamp()


def _get(run: Any, key: str) -> Any:
    return run.get(key) if isinstance(run, dict) else getattr(run, key, None)


def from_langsmith(runs: list[Any], run_id: str | None = None) -> Run:
    """A run from every run of one LangSmith trace (SDK ``Run`` objects or dicts with the same fields)."""
    by_id = {str(_get(r, "id")): r for r in runs}
    root = next(r for r in runs if not _get(r, "parent_run_id"))
    meta = (_get(root, "extra") or {}).get("metadata") or {}

    def is_tool(r: Any) -> bool:
        return _get(r, "run_type") == "tool"

    def has_tool_ancestor(r: Any) -> bool:
        return any(is_tool(by_id[str(p)]) for p in (_get(r, "parent_run_ids") or []) if str(p) in by_id)

    ordered = sorted(runs, key=lambda r: _ts(_get(r, "start_time")) or 0.0)
    calls = []
    for r in ordered:
        if _get(r, "run_type") != "llm":
            continue
        message = (((_get(r, "outputs") or {}).get("generations") or [[{}]])[0] or [{}])[0].get("message") or {}
        kwargs = message.get("kwargs") or {}
        calls.append(ModelCall(
            usage=kwargs.get("usage_metadata") or {}, tool_calls=[c["name"] for c in kwargs.get("tool_calls") or []],
            t_start=_ts(_get(r, "start_time")), t_end=_ts(_get(r, "end_time")), text=_text(kwargs.get("content")),
            langsmith_cost=_get(r, "total_cost"),
        ))  # fmt: skip
    results, parcs = [], []
    for r in ordered:
        if not is_tool(r):
            continue
        output = (_get(r, "outputs") or {}).get("output")
        if isinstance(output, dict):  # a ToolMessage
            status, text = output.get("status") or "success", _text(output.get("content"))
        else:  # MCP content blocks
            status, text = ("error" if _get(r, "error") else "success"), _text(output)
        if not has_tool_ancestor(r):
            results.append(ToolResult(_get(r, "name"), status, text))
        if _get(r, "name") in PARCS_TOOLS:
            parcs.append(ParcsCall(_get(r, "name"), _get(r, "inputs") or {}, _json(text), _ts(_get(r, "start_time")),
                                   _ts(_get(r, "end_time")), _get(r, "error")))  # fmt: skip
    info = next((p.envelope.get("output") or {} for p in parcs if p.name == "get_cluster_info"), {})
    start, end = _ts(_get(root, "start_time")), _ts(_get(root, "end_time"))
    return Run(
        run_id=run_id or str(_get(root, "id")), arm=meta.get("mode") or "?", model_name=meta.get("model") or "?",
        wall_s=end - start if start and end else None, termination="error" if _get(root, "error") else "answer",
        max_parallelism=info.get("maxParallelism"), calls=calls, tool_results=results, parcs=parcs,
        final_text=next((c.text for c in reversed(calls) if not c.tool_calls), ""),
    )  # fmt: skip
