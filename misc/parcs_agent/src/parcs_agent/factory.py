from __future__ import annotations

from typing import Any, Literal

from langchain.agents.middleware import AgentMiddleware
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_mcp_adapters.sessions import Connection

from .middleware import ParcsPTCMiddleware, ParcsToolCallingMiddleware

Mode = Literal["tool_calling", "ptc"]

_SERVER = "parcs"
_MIDDLEWARE: dict[str, type[AgentMiddleware]] = {
    "tool_calling": ParcsToolCallingMiddleware,
    "ptc": ParcsPTCMiddleware,
}


async def create_parcs_middleware(mode: Mode, connection: Connection, **options: Any) -> AgentMiddleware:
    """Connect to a PARCS MCP server and return the middleware that gives an agent its tools.

    ``mode`` is ``"tool_calling"`` (the model calls the PARCS tools directly, see
    :class:`ParcsToolCallingMiddleware`) or ``"ptc"`` (the model calls them from JavaScript in a
    QuickJS ``eval`` tool, see :class:`ParcsPTCMiddleware`).

    ``connection`` is any ``langchain-mcp-adapters`` connection, e.g.
    ``{"transport": "streamable_http", "url": "http://cluster:8080/", "headers": {...}}``.
    ``options`` go to the middleware: ``guidance`` for both modes, and for ``"ptc"`` also the
    ``CodeInterpreterMiddleware`` options (``timeout``, ``max_ptc_calls``, ``mode``, ...).
    """
    middleware = _MIDDLEWARE.get(mode)
    if middleware is None:
        raise ValueError(f"mode must be one of {sorted(_MIDDLEWARE)}, not {mode!r}")
    tools = await MultiServerMCPClient({_SERVER: connection}).get_tools()
    return middleware(tools, **options)  # type: ignore[call-arg]
