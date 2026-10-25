from __future__ import annotations

from langchain.agents import create_agent

from parcs_agent import ParcsPTCMiddleware, ParcsToolCallingMiddleware

from .conftest import scripted_model

SCRIPT = """{
const call = async (fn, args) => JSON.parse((await fn(args))[0].text);
const info = await call(tools.getClusterInfo, {});
if (!info.ok) throw new Error(JSON.stringify(info.error));
info.output.maxParallelism;
}"""


async def test_tool_calling_exposes_tool_and_guidance(cluster_info_tool):
    model = scripted_model({"name": "get_cluster_info", "args": {}})
    agent = create_agent(model, system_prompt="base", middleware=[ParcsToolCallingMiddleware([cluster_info_tool])])

    result = await agent.ainvoke({"messages": [("user", "go")]})

    assert "get_cluster_info" in model.bound_tools[0]
    assert model.system_prompts[0].startswith("base") and "## PARCS cluster" in model.system_prompts[0]
    assert "maxParallelism" in result["messages"][2].text


async def test_tool_calling_without_guidance_leaves_prompt_alone(cluster_info_tool):
    model = scripted_model()
    agent = create_agent(
        model, system_prompt="base", middleware=[ParcsToolCallingMiddleware([cluster_info_tool], guidance="")]
    )

    await agent.ainvoke({"messages": [("user", "go")]})

    assert model.system_prompts[0] == "base"


async def test_ptc_binds_only_eval_and_runs_script(cluster_info_tool):
    model = scripted_model({"name": "eval", "args": {"code": SCRIPT}})
    agent = create_agent(model, system_prompt="base", middleware=[ParcsPTCMiddleware([cluster_info_tool])])

    result = await agent.ainvoke({"messages": [("user", "go")]})

    assert model.bound_tools[0] == ["eval"]
    assert "## PARCS cluster" in model.system_prompts[0] and "JSON.parse" in model.system_prompts[0]
    assert result["messages"][2].text == "<result>4</result>"


def test_ptc_forwards_interpreter_options(cluster_info_tool):
    middleware = ParcsPTCMiddleware([cluster_info_tool], max_ptc_calls=3, timeout=5.0)

    assert middleware._max_ptc_calls == 3 and middleware._timeout == 5.0
