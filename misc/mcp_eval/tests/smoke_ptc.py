"""Smoke test of both orchestration arms against a running PARCS MCP server, without an LLM.

A scripted chat model plays the agent: in the ptc arm it sends one `eval` whose JavaScript
calls the PARCS tools; in the direct arm it calls get_cluster_info and create_session itself.
Checks that tools.* return the server envelope as native JS objects (errors included, no
throw) and that every call reaches the CallLog.

    MCP__CLUSTER_URL=http://127.0.0.1:18080 uv run python -m tests.smoke_ptc
"""

import asyncio
import json
from typing import Any

from langchain_core.language_models.fake_chat_models import FakeMessagesListChatModel
from langchain_core.messages import AIMessage, ToolMessage

from mcp_eval import agent as agent_module
from mcp_eval.tools import CallLog

PTC_SCRIPT = r"""
const info = await tools.getClusterInfo({});
const bad = await tools.createSession({ sourceCode: "return AgentLayerResult.Ok(missing);" });
const good = await tools.createSession({ sourceCode: 'return AgentLayerResult.Ok("{\\"v\\":1}");' });
const again = await tools.createSession({ sourceCode: 'return AgentLayerResult.Ok("{\\"v\\":1}");' });
const tooWide = await tools.runLayer({ sessionId: good.sessionId, parallelism: info.output.maxParallelism + 1 });
const layer = await tools.runLayer({ sessionId: good.sessionId, parallelism: 2, layerKey: "map-0", waitSeconds: 5 });
const layers = await tools.listLayers({ sessionId: good.sessionId });
JSON.stringify({
  mode: info.output.scalingMode, max: info.output.maxParallelism,
  badCode: bad.error.code, badLine: bad.error.diagnostics[0].line,
  reused: again.status, sameSession: again.sessionId === good.sessionId,
  tooWide: tooWide.error.code, layerOk: layer.ok, layerError: layer.error && layer.error.code,
  layerKey: layer.layerKey, listed: layers.output.layers.length,
});
"""


class ScriptedModel(FakeMessagesListChatModel):
    """Replays fixed responses; tool binding is a no-op."""

    def bind_tools(self, tools: Any, **kwargs: Any):  # noqa: ARG002
        return self


async def run(orchestration: str, responses: list[AIMessage]) -> tuple[list, CallLog]:
    agent_module.get_model = lambda *_: ScriptedModel(responses=responses)
    log = CallLog()
    agent = await agent_module.create_parcs_agent(orchestration, log)
    result = await agent.ainvoke({"messages": [("user", "smoke")]}, config={"configurable": {"thread_id": orchestration}})
    return result["messages"], log


async def main() -> None:
    messages, log = await run("ptc", [
        AIMessage(content="", tool_calls=[{"name": "eval", "args": {"code": PTC_SCRIPT}, "id": "e1"}]),
        AIMessage(content="done"),
    ])
    eval_result = next(m for m in messages if isinstance(m, ToolMessage) and m.name == "eval")
    print("ptc eval result:", eval_result.content)
    print("ptc log:", json.dumps(log.summary()))
    assert all(c.caller == "code" for c in log.calls) and len(log.calls) == 7, log.calls
    for expected in ('"badCode":"compile_error"', '"badLine":1', '"reused":"Reused"', '"sameSession":true',
                     '"tooWide":"capacity_exceeded"', '"layerKey":"map-0"', '"listed":', '"mode":"static"'):
        assert expected in eval_result.content, expected

    messages, log = await run("direct", [
        AIMessage(content="", tool_calls=[{"name": "get_cluster_info", "args": {}, "id": "d1"}]),
        AIMessage(content="", tool_calls=[{"name": "create_session", "args": {"sourceCode": "return AgentLayerResult.Ok(x);"}, "id": "d2"}]),
        AIMessage(content="done"),
    ])
    tool_results = [m.content for m in messages if isinstance(m, ToolMessage)]
    print("direct results:", tool_results)
    print("direct log:", json.dumps(log.summary()))
    assert json.loads(tool_results[0])["output"]["scalingMode"] == "static"
    assert json.loads(tool_results[1])["error"]["code"] == "compile_error"
    assert [c.caller for c in log.calls] == ["model", "model"]
    print("OK")


if __name__ == "__main__":
    asyncio.run(main())
