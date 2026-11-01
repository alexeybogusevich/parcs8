from __future__ import annotations

import json
import socket
import threading
import time
from collections.abc import Iterator
from typing import Any

import pytest
import uvicorn
from langchain_core.language_models.fake_chat_models import GenericFakeChatModel
from langchain_core.messages import AIMessage
from langchain_core.tools import BaseTool, StructuredTool
from mcp.server.fastmcp import FastMCP

CLUSTER_INFO = {"ok": True, "status": "Completed", "output": {"maxParallelism": 4}}


class FakeModel(GenericFakeChatModel):
    """Replays scripted messages and records the system prompt and bound tool names of each call."""

    system_prompts: list[str] = []
    bound_tools: list[list[str]] = []

    def bind_tools(self, tools: Any, **kwargs: Any) -> FakeModel:
        self.bound_tools.append([t.name for t in tools])
        return self

    def _generate(self, messages: Any, *args: Any, **kwargs: Any) -> Any:
        self.system_prompts.append(messages[0].text)
        return super()._generate(messages, *args, **kwargs)


def scripted_model(*tool_calls: dict[str, Any]) -> FakeModel:
    """A model that makes the given tool calls, one per turn, then answers ``done``."""
    messages = [AIMessage(content="", tool_calls=[{"id": str(i), **c}]) for i, c in enumerate(tool_calls)]
    return FakeModel(messages=iter([*messages, AIMessage(content="done")]), system_prompts=[], bound_tools=[])


@pytest.fixture
def cluster_info_tool() -> BaseTool:
    """Stands in for an MCP tool: the adapter returns a list of text content blocks."""

    async def get_cluster_info() -> list[dict[str, str]]:
        return [{"type": "text", "text": json.dumps(CLUSTER_INFO)}]

    return StructuredTool.from_function(coroutine=get_cluster_info, name="get_cluster_info", description="info")


@pytest.fixture(scope="session")
def mcp_url() -> Iterator[str]:
    """A real Streamable HTTP MCP server exposing one PARCS-like tool."""
    server = FastMCP("fake-parcs", stateless_http=True)

    @server.tool()
    def get_cluster_info() -> dict[str, Any]:
        """Cluster capacity."""
        return CLUSTER_INFO

    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        port = s.getsockname()[1]
    uv = uvicorn.Server(uvicorn.Config(server.streamable_http_app(), port=port, log_level="warning"))
    thread = threading.Thread(target=uv.run, daemon=True)
    thread.start()
    deadline = time.monotonic() + 10
    while not uv.started and time.monotonic() < deadline:
        time.sleep(0.05)
    assert uv.started, "MCP test server did not start"
    yield f"http://127.0.0.1:{port}/mcp"
    uv.should_exit = True
    thread.join(timeout=5)
