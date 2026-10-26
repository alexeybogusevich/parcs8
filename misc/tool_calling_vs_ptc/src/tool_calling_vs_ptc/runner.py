"""Executes one cell: a fresh agent and thread, the task, then the server's layer list and the grade.

Each run writes its full transcript (messages with usage and response metadata, the PARCS tool-call
log, model-call times, server layers) to ``transcripts/<run_id>.json`` and appends a compact record
to ``runs.jsonl``. Metrics are extracted later (phase 4) from those files.
"""

from __future__ import annotations

import asyncio
import json
import time
from pathlib import Path
from typing import Any
from uuid import uuid4

from langchain_core.messages import AIMessage, BaseMessage, messages_to_dict
from langgraph.errors import GraphRecursionError

from . import agent as agent_mod
from .cluster import Cluster, pool
from .config import ROOT, Config, ModelSpec
from .grading import grade
from .plan import Cell
from .tasks import LAYER_DETAILS, problem, render

TRUTH_FILE = ROOT / "tasks" / "ground_truth.json"

_INFRA_NAMES = frozenset(
    {
        "APIConnectionError",
        "APITimeoutError",
        "RateLimitError",
        "InternalServerError",
        "ServiceUnavailableError",
        "OverloadedError",
        "McpError",
        "ClosedResourceError",
        "BrokenResourceError",
    }
)


def _leaves(ex: BaseException):
    if isinstance(ex, BaseExceptionGroup):
        for inner in ex.exceptions:
            yield from _leaves(inner)
    else:
        yield ex


# Failures no later run can get past: stop the sweep instead of recording them cell after cell.
_FATAL_NAMES = frozenset({"AuthenticationError", "PermissionDeniedError", "NotFoundError"})
_FATAL_STATUS = frozenset({401, 402, 403, 404})


class SweepAborted(SystemExit):
    pass


def classify(ex: BaseException) -> str:
    """``infra_error`` (repeated later) for transport and provider-availability failures, else ``error``.
    Raises ``SweepAborted`` for a bad key, no credit or an unknown model."""
    import httpx

    for leaf in _leaves(ex):
        # By class hierarchy: langchain-openai re-raises as subclasses such as OpenAIRateLimitError.
        names = {c.__name__ for c in type(leaf).__mro__}
        status = getattr(leaf, "status_code", None)
        if names & _FATAL_NAMES or status in _FATAL_STATUS:
            raise SweepAborted(f"provider refused the run: {type(leaf).__name__}: {leaf}")
        if (
            isinstance(leaf, httpx.HTTPError | ConnectionError)
            or names & _INFRA_NAMES
            or status == 429
            or (status or 0) >= 500
        ):
            return "infra_error"
    return "error"


def blame(termination: str, result: dict, log: agent_mod.RunLog, llm_calls: int) -> str:
    """Runs to repeat instead of charging to the model: one where the model never answered, and a broken
    plan next to a lost tool call (a retried call can start a layer twice, a lost response hides one)."""
    if llm_calls == 0:
        return "infra_error"
    if termination == "answer" and not result["protocol_ok"] and log.transport_errors():
        return "infra_error"
    return termination


def truth(family: str, n: int) -> float:
    return json.loads(TRUTH_FILE.read_text(encoding="utf-8"))[problem(family)]["per_n"][str(n)]["answer"]


def final_text(messages: list[BaseMessage]) -> str:
    for msg in reversed(messages):
        if isinstance(msg, AIMessage) and not msg.tool_calls:
            return msg.text
    return ""


def usage(messages: list[BaseMessage]) -> list[dict]:
    """Per model call: token usage and the provider's response metadata."""
    return [
        {"usage": m.usage_metadata, "response_metadata": m.response_metadata, "tool_calls": len(m.tool_calls)}
        for m in messages
        if isinstance(m, AIMessage)
    ]


def run_config(cell: Cell, spec: ModelSpec, manifest_hash: str, recursion_limit: int) -> dict[str, Any]:
    """A fresh thread per run: ``ThreadNonceMiddleware`` keys its cache-busting nonce on ``thread_id``."""
    return {
        "configurable": {"thread_id": str(uuid4())},
        "run_id": uuid4(),
        "recursion_limit": recursion_limit,
        "callbacks": [agent_mod.StreamCounter()],
        "run_name": cell.run_id,
        "tags": [cell.family, f"n{cell.n}", cell.arm, cell.model],
        "metadata": {**cell.as_dict(), "model_name": spec.model_name, "manifest": manifest_hash},
    }


async def drive(agent: Any, text: str, config: dict, budget: float) -> tuple[list[BaseMessage], str, str | None]:
    """Run the agent; returns the last message state, the termination reason and the error text."""
    messages: list[BaseMessage] = []
    try:
        async with asyncio.timeout(budget):
            async for state in agent.astream({"messages": [("user", text)]}, config=config, stream_mode="values"):
                messages = state.get("messages") or messages
        return messages, "answer", None
    except TimeoutError:
        return messages, "wall_budget", None
    except GraphRecursionError as ex:
        return messages, "recursion_limit", str(ex)
    except Exception as ex:  # noqa: BLE001 - every failure is a recorded outcome
        return messages, classify(ex), f"{type(ex).__name__}: {ex}"


async def execute(
    cell: Cell, config: Config, spec: ModelSpec, cluster: Cluster, manifest: dict, manifest_hash: str, results_dir: Path
) -> dict:
    exp = config.experiment
    info = await cluster.wait_idle(exp.idle_timeout_seconds)
    if pool(info) != manifest["pool"]:
        raise SystemExit(f"the pool changed since the manifest was written: {pool(info)} != {manifest['pool']}")

    task = render(cell.family, cell.n)
    run_cfg = run_config(cell, spec, manifest_hash, exp.recursion_limit)
    log = agent_mod.RunLog(cluster_url=config.mcp.cluster_url)
    agent_mod.RECORDER.current = log
    started = time.time()
    try:
        try:
            agent = await agent_mod.create_run_agent(
                config.mcp.cluster_url, spec.llm(config.llm), cell.arm, exp.ptc_options(), spec.extra_body()
            )
        except Exception as ex:  # noqa: BLE001
            messages, termination, error = [], classify(ex), f"{type(ex).__name__}: {ex}"
        else:
            messages, termination, error = await drive(agent, task.text, run_cfg, exp.wall_budget_seconds)
    finally:
        agent_mod.RECORDER.current = None
    wall = time.time() - started
    if termination == "wall_budget" and (hung := log.hung(time.time())):
        # The budget ran out while the server sat on a call: the transport's fault, not the model's.
        termination = "infra_error"
        error = "hung tool call: " + ", ".join(f"{c['name']} ({time.time() - c['t_start']:.0f} s)" for c in hung)

    try:
        layers = await cluster.layers(log.session_ids())
        cancelled = await cluster.cancel_unfinished(layers)
        if cell.family in LAYER_DETAILS:
            await cluster.add_details(layers)
    except Exception as ex:  # noqa: BLE001 - without the server's layer list the run cannot be graded
        layers, cancelled = [], []
        termination, error = "infra_error", f"list_layers: {type(ex).__name__}: {ex}"
    text = final_text(messages) if termination == "answer" else ""
    result = grade(
        cell.family, cell.n, text, layers, log.tool_calls, truth(cell.family, cell.n), info["maxParallelism"]
    )
    ai = usage(messages)
    termination = blame(termination, result, log, len(ai))

    transcript = results_dir / "transcripts" / f"{cell.run_id}.json"
    transcript.parent.mkdir(parents=True, exist_ok=True)
    transcript.write_text(
        json.dumps(
            {
                "cell": cell.as_dict(),
                "task": task.text,
                "config": {k: v for k, v in run_cfg.items() if k not in ("run_id", "callbacks")},
                "trace_id": str(run_cfg["run_id"]),
                "cluster_info": info,
                "messages": messages_to_dict(messages),
                "tool_calls": log.tool_calls,
                "model_calls": log.model_calls,
                "layers": layers,
                "cancelled": cancelled,
                "termination": termination,
                "error": error,
                "grade": result,
            },
            default=str,
            indent=1,
        ),
        encoding="utf-8",
    )

    # Only now, with the transcript and the server's layer list saved, are the run's sessions disposable.
    try:
        cleanup_failures = await cluster.delete_sessions(log.session_ids())
    except Exception as ex:  # noqa: BLE001 - a failed cleanup never fails a run that was already graded
        cleanup_failures = [f"{type(ex).__name__}: {ex}"]

    return {
        **cell.as_dict(),
        "manifest": manifest_hash,
        "trace_id": str(run_cfg["run_id"]),
        "thread_id": run_cfg["configurable"]["thread_id"],
        "started_at": time.strftime("%Y-%m-%dT%H:%M:%S%z", time.localtime(started)),
        "wall_s": wall,
        "termination": termination,
        "error": error,
        **{k: v for k, v in result.items() if k != "final_output"},
        "llm_calls": len(ai),
        "input_tokens": sum((m["usage"] or {}).get("input_tokens", 0) for m in ai),
        "output_tokens": sum((m["usage"] or {}).get("output_tokens", 0) for m in ai),
        "parcs_tool_calls": len(log.tool_calls),
        "transport_errors": len(log.transport_errors()),
        "transcript": transcript.relative_to(results_dir).as_posix(),
        "cleanup_failures": cleanup_failures,
    }
