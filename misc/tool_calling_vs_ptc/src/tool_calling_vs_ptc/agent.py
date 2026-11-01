"""The agent of one arm, as ``parcs_agent_cli`` builds it, plus recording.

Every PARCS tool call passes through ``record_tool_call``, an MCP tool interceptor. It sits under
both arms: a direct tool call in ``tool_calling`` and a ``tools.*`` call from inside ``eval`` in
``ptc`` take the same path, so both arms are graded from the same log. ``ModelTimingMiddleware``
times every model call.

Each run sees the server as if it were alone on it (``private_view``):

- The server reuses a session for identical source code, and with it every keyed layer of that
  session. At temperature 0 repetitions often send identical C#, so ``create_session`` gets a per-run
  comment appended: each run compiles its own session and runs its own layers.
- The server keeps every earlier run's sessions, and ``list_sessions`` lists them all, so a run could
  read another run's results. ``list_sessions`` is filtered to the run's own sessions, and a call
  naming another run's session or layer gets the server's own not-found error. A layer is the run's
  own when it belongs to one of its sessions, whatever state it is in, so a failed layer's
  diagnostics stay readable.
"""

from __future__ import annotations

import asyncio
import copy
import json
import time
from collections.abc import Awaitable, Callable
from dataclasses import dataclass, field
from typing import Any
from uuid import uuid4

from eval_utils import LLMSettings, ThreadNonceMiddleware, create_model
from langchain.agents import create_agent
from langchain.agents.middleware import AgentMiddleware, ModelRequest, ModelResponse, ToolRetryMiddleware
from langchain_core.callbacks import AsyncCallbackHandler
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_mcp_adapters.interceptors import MCPToolCallRequest
from langchain_mcp_adapters.sessions import Connection
from mcp.types import CallToolResult, TextContent
from parcs_agent import Mode, ParcsPTCMiddleware, ParcsToolCallingMiddleware

_SERVER = "parcs"
# A PARCS call is hung once it has gone unanswered for its own wait (waitSeconds, at least the floor)
# plus the grace: the server never blocks longer than waitSeconds.
HUNG_FLOOR_SECONDS = 60.0
HUNG_GRACE_SECONDS = 60.0
_ENVELOPE_FIELDS = ("ok", "status", "sessionId", "layerId", "layerKey", "reused", "layer", "output", "outputs", "error")


@dataclass
class RunLog:
    """What one run did: every PARCS tool call and every model call, with wall-clock times."""

    tool_calls: list[dict] = field(default_factory=list)
    model_calls: list[dict] = field(default_factory=list)
    nonce: str = field(default_factory=lambda: uuid4().hex)
    # Where to ask for a session's layers when a layerId is unknown (its start response was lost).
    cluster_url: str | None = None
    own_sessions: dict[str, None] = field(default_factory=dict)  # insertion-ordered set
    own_layers: set[str] = field(default_factory=set)
    # PARCS calls sent and not yet answered, by id of their log entry.
    in_flight: dict[int, dict] = field(default_factory=dict)

    def hung(self, now: float) -> list[dict]:
        """Calls sent and never answered, past the point where the server must have replied."""
        return [
            c
            for c in self.in_flight.values()
            if now - c["t_start"]
            > max(float((c["args"] or {}).get("waitSeconds") or 0), HUNG_FLOOR_SECONDS) + HUNG_GRACE_SECONDS
        ]

    def session_ids(self) -> list[str]:
        """The sessions this run created."""
        return list(self.own_sessions)

    def remember(self, name: str, env: dict) -> None:
        """Track the run's sessions, and every layer seen in one of them, failed ones included."""
        if name == "create_session" and env.get("ok") and env.get("sessionId"):
            self.own_sessions[env["sessionId"]] = None
        # A layer's output is whatever its computation returned: an object, a list, a bare number, ...
        output = env.get("output")
        output = output if isinstance(output, dict) else {}
        if env.get("sessionId") in self.own_sessions:
            if env.get("layerId"):
                self.own_layers.add(env["layerId"])
            for layer in output.get("layers") or []:  # list_layers
                self.own_layers.add(layer["layerId"])
        for session in output.get("sessions") or []:  # list_sessions, already filtered
            self.own_layers.update(layer["layerId"] for layer in session.get("layers") or [])

    async def refresh(self) -> None:
        """Learn every layer of the run's sessions from the server."""
        if self.cluster_url:
            from .cluster import Cluster

            for row in await Cluster(self.cluster_url).layers(self.session_ids()):
                self.own_layers.add(row["layerId"])

    def transport_errors(self) -> list[dict]:
        return [c for c in self.tool_calls if c.get("exception")]


class _Recorder:
    """The log of the run in progress. Runs are sequential, so one slot is enough."""

    current: RunLog | None = None


RECORDER = _Recorder()


def _envelope(result: Any) -> dict | None:
    structured = getattr(result, "structuredContent", None)
    if isinstance(structured, dict):
        return structured
    for block in getattr(result, "content", None) or []:
        text = getattr(block, "text", None)
        if text:
            try:
                parsed = json.loads(text)
            except ValueError:
                return {"text": text[:2000]}
            return parsed if isinstance(parsed, dict) else {"value": parsed}
    return None


def own_session(request: MCPToolCallRequest, nonce: str) -> MCPToolCallRequest:
    """``create_session`` with a trailing per-run comment (trailing, so diagnostic line numbers hold)."""
    source = request.args.get("sourceCode")
    if request.name != "create_session" or not isinstance(source, str):
        return request
    return request.override(args={**request.args, "sourceCode": f"{source.rstrip()}\n// run {nonce}\n"})


def _result(env: dict) -> CallToolResult:
    """A tool result shaped like the server's (``Respond`` in ParcsAgentTools.cs)."""
    text = json.dumps(env, separators=(",", ":"))
    return CallToolResult(content=[TextContent(type="text", text=text)], structuredContent=env, isError=not env["ok"])


def own_sessions_only(result: Any, log: RunLog) -> Any:
    env = _envelope(result)
    sessions = ((env or {}).get("output") or {}).get("sessions")
    if not isinstance(sessions, list):
        return result
    env = copy.deepcopy(env)
    env["output"]["sessions"] = [s for s in sessions if s.get("sessionId") in log.own_sessions]
    return _result(env)


def _not_found(code: str, message: str) -> CallToolResult:
    return _result({"ok": False, "error": {"code": code, "message": message, "retryable": False}})


async def private_view(request: MCPToolCallRequest, handler, log: RunLog) -> tuple[Any, bool]:
    """The call as a server holding only this run's sessions would answer it; True when it was refused."""
    args = request.args
    session = args.get("sessionId")
    if session and session not in log.own_sessions:
        return _not_found("session_not_found", f"Session '{session}' not found."), True
    # A parent must be checked before the call: once started, a layer reading another run's results
    # cannot be taken back.
    unknown = [p for p in args.get("previousLayerIds") or [] if p not in log.own_layers]
    if unknown:
        await log.refresh()
        unknown = [p for p in unknown if p not in log.own_layers]
        if unknown:
            return _not_found("layer_not_found", f"previousLayerIds: layer '{unknown[0]}' not found."), True
    result = await handler(request)
    if request.name == "list_sessions":
        return own_sessions_only(result, log), False
    # A call naming only a layer (wait_layer, get_layer_result, cancel_layer) is judged by its answer.
    answered = (_envelope(result) or {}).get("sessionId")
    if args.get("layerId") and answered and answered not in log.own_sessions:
        return _not_found("layer_not_found", f"Layer '{args['layerId']}' not found."), True
    return result, False


async def record_tool_call(request: MCPToolCallRequest, handler: Callable[[MCPToolCallRequest], Awaitable[Any]]):
    log = RECORDER.current
    if log is None:
        return await handler(request)
    request = own_session(request, log.nonce)
    entry: dict[str, Any] = {"name": request.name, "args": request.args, "t_start": time.time()}
    log.in_flight[id(entry)] = entry
    try:
        result, refused = await private_view(request, handler, log)
    except BaseException as ex:
        entry.update(t_end=time.time(), exception=f"{type(ex).__name__}: {ex}")
        log.tool_calls.append(entry)
        # A call cancelled by the wall budget stays in flight, so the runner can tell a hang from a slow model.
        if not isinstance(ex, asyncio.CancelledError):
            log.in_flight.pop(id(entry), None)
        raise
    log.in_flight.pop(id(entry), None)
    env = _envelope(result) or {}
    log.remember(request.name, env)
    entry.update(
        t_end=time.time(),
        is_error=bool(getattr(result, "isError", False)),
        envelope={k: env[k] for k in _ENVELOPE_FIELDS if k in env} if "ok" in env else env,
    )
    if refused:
        entry["foreign"] = True
    log.tool_calls.append(entry)
    return result


class ModelTimingMiddleware(AgentMiddleware):
    """Records start and end of every model call. Placed last, so it times the model alone.

    The entry is logged when the call starts, so a call cut off by the wall budget is still there,
    with what ``StreamCounter`` saw arrive: chunks mean the model was generating, none a stalled provider.
    """

    async def awrap_model_call(
        self, request: ModelRequest[Any], handler: Callable[[ModelRequest[Any]], Awaitable[ModelResponse[Any]]]
    ) -> ModelResponse[Any]:
        entry: dict[str, Any] = {"t_start": time.time(), "chunks": 0, "chars": 0, "t_first_chunk": None}
        if RECORDER.current is not None:
            RECORDER.current.model_calls.append(entry)
        try:
            return await handler(request)
        finally:
            entry["t_end"] = time.time()


class StreamCounter(AsyncCallbackHandler):
    """Counts the streamed chunks (and their text and tool-call argument characters) of the model call in progress."""

    async def on_llm_new_token(self, token: str, *, chunk: Any = None, **kwargs: Any) -> None:
        log = RECORDER.current
        if log is None or not log.model_calls:
            return
        entry = log.model_calls[-1]
        entry["chunks"] += 1
        entry["t_first_chunk"] = entry["t_first_chunk"] or time.time()
        message = getattr(chunk, "message", None)
        args = sum(len(c.get("args") or "") for c in getattr(message, "tool_call_chunks", None) or [])
        entry["chars"] += len(token or "") + args


async def parcs_tools(cluster_url: str):
    """The PARCS MCP tools, every call recorded. Same client as ``parcs_agent.create_parcs_middleware``."""
    connection: Connection = {"transport": "streamable_http", "url": cluster_url.rstrip("/") + "/"}
    return await MultiServerMCPClient({_SERVER: connection}, tool_interceptors=[record_tool_call]).get_tools()


def parcs_middleware(arm: Mode, tools: list, ptc_options: dict) -> Any:
    if arm == "ptc":
        return ParcsPTCMiddleware(tools, **ptc_options)
    if arm == "tool_calling":
        return ParcsToolCallingMiddleware(tools)
    raise ValueError(f"unknown arm {arm!r}")


def build_agent(model: Any, arm: Mode, tools: list, ptc_options: dict):
    """The middleware stack of ``parcs_agent_cli.agent``, with model timing innermost."""
    return create_agent(
        name="ParcsAgent",
        model=model,
        middleware=[
            ToolRetryMiddleware(max_retries=3, backoff_factor=2.0, initial_delay=1.0),
            ThreadNonceMiddleware(),
            parcs_middleware(arm, tools, ptc_options),
            ModelTimingMiddleware(),
        ],
    )


async def create_run_agent(
    cluster_url: str, llm: LLMSettings, arm: Mode, ptc_options: dict, extra_body: dict | None = None
):
    model = create_model(llm)
    if extra_body:
        model = model.model_copy(update={"extra_body": {**(getattr(model, "extra_body", None) or {}), **extra_body}})
    return build_agent(model, arm, await parcs_tools(cluster_url), ptc_options)
