"""PARCS MCP tools for the agent.

The server's tools are loaded over Streamable HTTP and wrapped so that every call returns the
server's result envelope ``{ok, status, sessionId?, layerId?, layer?, output?, outputs?,
workers?, error?}``:

- as a dict when code calls it from the QuickJS interpreter (PTC) — a native JS object, so a
  script checks ``r.ok`` / ``r.status`` without ``JSON.parse`` and errors do not throw;
- as compact JSON text when the model calls it directly.

Both orchestration arms therefore see the same tool names, descriptions, argument schemas and
result shape — only the caller differs. Every forwarded call is recorded in a ``CallLog``.
"""

from __future__ import annotations

import json
import time
from dataclasses import dataclass, field
from enum import StrEnum
from typing import Any
from uuid import uuid4

from langchain_core.messages import ToolMessage
from langchain_core.tools import BaseTool, StructuredTool
from langchain_mcp_adapters.client import MultiServerMCPClient

from .config import config


class ParcsMCPToolNames(StrEnum):
    """Tool names exposed by the PARCS MCP server."""

    GetClusterInfo = "get_cluster_info"
    CreateSession = "create_session"
    RunLayer = "run_layer"
    SubmitLayer = "submit_layer"
    WaitLayer = "wait_layer"
    GetLayerResult = "get_layer_result"
    ListSessions = "list_sessions"
    ListLayers = "list_layers"
    CancelLayer = "cancel_layer"


@dataclass
class ToolCallRecord:
    tool: str
    caller: str  # "model" or "code"
    started_at: float
    seconds: float
    ok: bool | None
    status: str | None
    error_code: str | None
    layer_id: str | None
    layer_key: str | None
    reused: bool
    result_chars: int


@dataclass
class CallLog:
    """Every PARCS tool call of one run, in order."""

    calls: list[ToolCallRecord] = field(default_factory=list)

    def summary(self) -> dict[str, Any]:
        layers_started = {
            c.layer_id
            for c in self.calls
            if c.tool in (ParcsMCPToolNames.RunLayer, ParcsMCPToolNames.SubmitLayer) and c.layer_id and not c.reused
        }
        return {
            "tool_calls": len(self.calls),
            "tool_calls_from_code": sum(c.caller == "code" for c in self.calls),
            "tool_errors": sum(c.ok is False for c in self.calls),
            "layers_started": len(layers_started),
            # Repeats of a layerKey that the server answered with the existing layer.
            "duplicate_layer_requests": sum(c.reused for c in self.calls),
            "compile_errors": sum(c.error_code == "compile_error" for c in self.calls),
            "tool_seconds": round(sum(c.seconds for c in self.calls), 3),
        }


def _envelope_from(message: Any) -> dict[str, Any]:
    """Extract the server envelope from an MCP tool's ToolMessage."""
    if isinstance(message, ToolMessage):
        artifact = message.artifact
        structured = getattr(artifact, "structured_content", None)
        if structured is None and isinstance(artifact, dict):
            structured = artifact.get("structured_content")
        if isinstance(structured, dict):
            return structured
        content = message.content
    else:
        content = message

    if isinstance(content, list):
        content = "".join(
            block.get("text", "") if isinstance(block, dict) else str(block) for block in content
        )
    try:
        parsed = json.loads(content)
        if isinstance(parsed, dict):
            return parsed
    except (TypeError, json.JSONDecodeError):
        pass
    return {"ok": False, "error": {"code": "client_error", "message": str(content), "retryable": False}}


def _wrap(mcp_tool: BaseTool, log: CallLog, max_result_chars: int) -> BaseTool:
    """Same name, description and schema as the server tool; returns the envelope."""

    async def call(caller: str, **kwargs: Any) -> dict[str, Any]:
        started = time.time()
        message = await mcp_tool.ainvoke(
            {"type": "tool_call", "id": f"call_{uuid4().hex}", "name": mcp_tool.name, "args": kwargs}
        )
        envelope = _envelope_from(message)
        error = envelope.get("error") or {}
        text = json.dumps(envelope, separators=(",", ":"), ensure_ascii=False)
        log.calls.append(
            ToolCallRecord(
                tool=mcp_tool.name,
                caller=caller,
                started_at=started,
                seconds=round(time.time() - started, 3),
                ok=envelope.get("ok"),
                status=envelope.get("status"),
                error_code=error.get("code"),
                layer_id=envelope.get("layerId"),
                layer_key=envelope.get("layerKey"),
                reused=bool(envelope.get("reused")),
                result_chars=len(text),
            )
        )
        return envelope

    async def for_model(**kwargs: Any) -> str:
        text = json.dumps(await call("model", **kwargs), separators=(",", ":"), ensure_ascii=False)
        # The same cap as the interpreter applies to eval results, so both arms see as much.
        if len(text) > max_result_chars:
            text = text[:max_result_chars] + f"…[truncated {len(text) - max_result_chars} chars; use view or get_layer_result]"
        return text

    async def for_code(**kwargs: Any) -> dict[str, Any]:
        return await call("code", **kwargs)

    tool = StructuredTool.from_function(
        coroutine=for_model,
        name=mcp_tool.name,
        description=mcp_tool.description,
        args_schema=mcp_tool.args_schema,
    )
    # The PTC bridge passes a dict through to JS as a native object; mark the variant so the
    # agent factory can hand the interpreter the code-facing coroutine.
    tool.metadata = {"parcs_for_code": StructuredTool.from_function(
        coroutine=for_code,
        name=mcp_tool.name,
        description=mcp_tool.description,
        args_schema=mcp_tool.args_schema,
    )}
    return tool


async def get_parcs_mcp_tools(log: CallLog | None = None) -> list[BaseTool]:
    """Load the PARCS tools from the configured server and wrap them (see module docs)."""
    if not config.mcp.cluster_url:
        raise ValueError("Cluster URL must be provided in the configuration")

    url = config.mcp.cluster_url.rstrip("/") + "/"
    client = MultiServerMCPClient({"parcs": {"transport": "streamable_http", "url": url}})
    log = log if log is not None else CallLog()
    return [_wrap(t, log, config.ptc.max_result_chars) for t in await client.get_tools()]


def code_variants(tools: list[BaseTool]) -> list[BaseTool]:
    """The interpreter-facing variants of wrapped PARCS tools (dict results)."""
    return [t.metadata["parcs_for_code"] for t in tools if t.metadata and "parcs_for_code" in t.metadata]
