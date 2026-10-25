from __future__ import annotations

import hashlib
import json
import socket
import threading
import time
from collections.abc import Iterator
from typing import Any

import pytest
import uvicorn
from langchain_core.language_models import BaseChatModel
from langchain_core.messages import AIMessage, ToolMessage
from langchain_core.outputs import ChatGeneration, ChatResult
from mcp.server.fastmcp import FastMCP

CLUSTER_INFO = {
    "scalingMode": "static",
    "workerNodeCount": 1,
    "currentCapacity": 4,
    "maxParallelism": 4,
    "daemonCpuRequestMillicores": 250,
    "capacitySource": "dns",
    "freeWorkers": 4,
    "runningLayers": 0,
    "queuedLayers": 0,
}


class ScriptModel(BaseChatModel):
    """Answers each call with ``script(messages)`` and records the system prompt of each call."""

    script: Any
    system_prompts: list[str] = []

    @property
    def _llm_type(self) -> str:
        return "script"

    def bind_tools(self, tools: Any, **kwargs: Any) -> ScriptModel:
        return self

    def _generate(self, messages: Any, *args: Any, **kwargs: Any) -> ChatResult:
        self.system_prompts.append(messages[0].text)
        return ChatResult(generations=[ChatGeneration(message=self.script(messages))])


def ai(turn: list[dict] | str, turn_index: int = 0) -> AIMessage:
    """A list of tool calls (made in parallel), or the final text."""
    if isinstance(turn, str):
        return AIMessage(content=turn)
    return AIMessage(content="", tool_calls=[{"id": f"c{turn_index}-{i}", **c} for i, c in enumerate(turn)])


def scripted(*turns: list[dict] | str) -> ScriptModel:
    """Replays the turns in order, one per model call."""
    queue = iter(enumerate(turns))

    def step(messages: list) -> AIMessage:
        index, turn = next(queue)
        return ai(turn, index)

    return ScriptModel(script=step, system_prompts=[])


def last_envelope(messages: list, tool: str) -> dict:
    """The envelope of the latest result of ``tool`` in the conversation."""
    for msg in reversed(messages):
        if isinstance(msg, ToolMessage) and msg.name == tool:
            content = msg.content
            text = content[0]["text"] if isinstance(content, list) else content
            return json.loads(text)
    raise LookupError(tool)


class FakeParcs:
    """Layers complete at once. `Sub` outputs {value: parameters.v}; `Sum` adds its parents' values.
    Like the real server, identical source reuses a session, and a repeated layerKey its layer."""

    def __init__(self) -> None:
        self.layers: dict[str, dict] = {}

    def run_layer(self, sessionId: str, entryPoint: str, parallelism: int, layerKey: str,
                  parameters: dict | None = None, previousLayerIds: list[str] | None = None) -> dict:  # fmt: skip
        prev = previousLayerIds or []
        layer_id = f"{sessionId}/{layerKey}"
        if layer_id in self.layers:
            return {**self.layers[layer_id]["envelope"], "reused": True}
        if entryPoint == "Hang":  # a server that sits on the call
            time.sleep(3)
        if entryPoint == "Fail":
            layer = {"parallelism": parallelism, "previousLayerIds": prev}
            error = {"code": "worker_error", "message": "1 of 1 workers failed. First error: boom", "retryable": False}
            envelope = {"ok": False, "status": "Failed", "sessionId": sessionId, "layerId": layer_id,
                        "layerKey": layerKey, "layer": layer, "error": error}  # fmt: skip
            row = {"layerId": layer_id, "layerKey": layerKey, "status": "Failed", "entryPoint": entryPoint, **layer}
            self.layers[layer_id] = {"sessionId": sessionId, "output": None, "envelope": envelope, "row": row}
            return envelope
        if entryPoint == "Sub":
            output = {"value": float((parameters or {})["v"])}
        else:
            output = {"total": sum((self.layers[p]["output"] or {}).get("value", 0) for p in prev)}
        layer = {"parallelism": parallelism, "previousLayerIds": prev}
        envelope = {"ok": True, "status": "Completed", "sessionId": sessionId, "layerId": layer_id,
                    "layerKey": layerKey, "layer": layer, "output": output}  # fmt: skip
        row = {"layerId": layer_id, "layerKey": layerKey, "status": "Completed", "entryPoint": entryPoint, **layer}
        self.layers[layer_id] = {"sessionId": sessionId, "output": output, "envelope": envelope, "row": row}
        return envelope

    def server(self) -> FastMCP:
        mcp = FastMCP("fake-parcs", stateless_http=True, streamable_http_path="/")

        @mcp.tool()
        def get_cluster_info() -> dict:
            """Cluster capacity."""
            return {"ok": True, "output": CLUSTER_INFO}

        @mcp.tool()
        def create_session(sourceCode: str) -> dict:
            """Compile a session."""
            digest = hashlib.sha256(sourceCode.replace("\r\n", "\n").strip().encode()).hexdigest()[:12]
            return {"ok": True, "status": "Created", "sessionId": f"s-{digest}"}

        @mcp.tool()
        def run_layer(sessionId: str, entryPoint: str, parallelism: int, layerKey: str,
                      parameters: dict | None = None, previousLayerIds: list[str] | None = None) -> dict:  # fmt: skip
            """Run a layer."""
            return self.run_layer(sessionId, entryPoint, parallelism, layerKey, parameters, previousLayerIds)

        @mcp.tool()
        def list_layers(sessionId: str) -> dict:
            """A session's layers."""
            rows = [layer["row"] for layer in self.layers.values() if layer["sessionId"] == sessionId]
            return {"ok": True, "sessionId": sessionId, "output": {"layers": rows}}

        @mcp.tool()
        def cancel_layer(layerId: str) -> dict:
            """Cancel a layer."""
            return {"ok": True, "layerId": layerId, "status": "Cancelled"}

        @mcp.tool()
        def list_sessions() -> dict:
            """Every session on the server, with its layers."""
            sessions = sorted({layer["sessionId"] for layer in self.layers.values()})
            rows = [{"sessionId": sid, "layers": [{"layerId": lid} for lid, layer in self.layers.items()
                                                  if layer["sessionId"] == sid]} for sid in sessions]  # fmt: skip
            return {"ok": True, "output": {"sessions": rows}}

        @mcp.tool()
        def get_layer_result(layerId: str) -> dict:
            """A layer's result."""
            if layerId not in self.layers:
                return {"ok": False, "error": {"code": "layer_not_found", "message": "not found", "retryable": False}}
            return self.layers[layerId]["envelope"]

        return mcp


@pytest.fixture(scope="session")
def fake_parcs() -> Iterator[tuple[str, FakeParcs]]:
    """A real Streamable HTTP MCP server with the PARCS tools the runner and the tests use."""
    parcs = FakeParcs()
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        port = s.getsockname()[1]
    uv = uvicorn.Server(uvicorn.Config(parcs.server().streamable_http_app(), port=port, log_level="warning"))
    thread = threading.Thread(target=uv.run, daemon=True)
    thread.start()
    deadline = time.monotonic() + 10
    while not uv.started and time.monotonic() < deadline:
        time.sleep(0.05)
    assert uv.started, "MCP test server did not start"
    yield f"http://127.0.0.1:{port}", parcs
    uv.should_exit = True
    thread.join(timeout=5)
