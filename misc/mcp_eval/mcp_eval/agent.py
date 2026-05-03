from __future__ import annotations

import os
from typing import Any

from langchain_core.language_models import BaseChatModel

from .python_exec import python_exec
from .tools import get_parcs_mcp_tools
from .config import config

# ── System prompts ─────────────────────────────────────────────────────────────

_PARALLEL_SYSTEM = """You are an agent connected to a PARCS distributed compute cluster.

MANDATORY RULES — violation means evaluation failure:
1. You MUST call get_cluster_info, then create_session, then run_layer for EVERY task.
2. You are FORBIDDEN from providing numerical answers without first running the cluster job.
3. Do NOT answer from memory or training knowledge under any circumstances.
4. Every number you report must come from a run_layer result in this session.

Workflow:
1. Call get_cluster_info to find maxParallelism.
2. Write C# code implementing IAgentComputation (one class, ExecuteAsync method).
3. Call create_session with that code. Fix compilation errors and retry.
4. Call run_layer with parallelism=maxParallelism. Read totalElapsedSeconds from resultJson.
5. If an aggregation layer is needed, run it with parallelism=1.
6. Output the JSON block requested in the task.

The C# interface:
  public interface IAgentComputation {
      Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);
  }
Each worker gets: input.WorkerIndex, input.TotalWorkers, input.Parameters, input.DatasetPath.
Return: AgentLayerResult.Ok(JsonSerializer.Serialize(yourObject));
"""

_SEQUENTIAL_SYSTEM = """You are an AI assistant that solves computational problems by writing and running Python code.

MANDATORY RULES — violation means evaluation failure:
1. You MUST call python_exec for EVERY task. No exceptions.
2. You are FORBIDDEN from providing numerical answers without first running the code.
3. Do NOT answer from memory or training knowledge under any circumstances.
4. Every number you report must come from a python_exec call in this session.

How to work:
1. Write complete Python code that solves the problem (use numpy, scipy as needed).
2. At the END of your script always print:
     import time
     # ... your computation ...
     print(f"elapsed_seconds: {elapsed:.3f}")
     print(f"result: {result}")
3. Call python_exec with that code.
4. If it fails, fix and retry.
5. Output the JSON block requested in the task.
"""


# ── Model factory ──────────────────────────────────────────────────────────────

def get_model(model_name: str | None = None) -> BaseChatModel:
    name  = model_name or config.llm.model_name
    clean = name.removeprefix("google/").removeprefix("publishers/google/models/")

    if config.llm.provider == "vertexai":
        from langchain_google_vertexai import ChatVertexAI  # type: ignore
        return ChatVertexAI(
            model_name=clean,
            project=config.llm.project or None,
            location=config.llm.location,
            temperature=config.llm.temperature,
            streaming=True,
        )

    if config.llm.provider == "google":
        from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore
        return ChatGoogleGenerativeAI(
            model=clean,
            temperature=config.llm.temperature,
            google_api_key=config.llm.api_key or None,
        )

    if config.llm.provider == "anthropic":
        from langchain_anthropic import ChatAnthropic  # type: ignore
        return ChatAnthropic(
            model_name=name,
            temperature=config.llm.temperature,
            api_key=config.llm.api_key or None,  # type: ignore
            streaming=True,
        )

    from langchain_openai import ChatOpenAI
    return ChatOpenAI(
        model=name,
        temperature=config.llm.temperature,
        base_url=config.llm.base_url,
        api_key=config.llm.api_key,  # type: ignore
        stream_usage=True,
        streaming=True,
    )


def _make_react_agent(model: BaseChatModel, tools: list):
    """Create a react agent compatible with all installed LangGraph versions."""
    from langgraph.prebuilt import create_react_agent  # type: ignore
    return create_react_agent(model=model, tools=tools)


# ── Forced-first-call executor ─────────────────────────────────────────────────
# Gemini thinking models skip tools when confident they know the answer.
# Solution: bind with tool_choice on turn 1 to guarantee at least one tool call,
# then release on turn 2+ so the model can give the final answer normally.

async def forced_tool_astream(
    model: BaseChatModel,
    tools: list,
    system: str,
    prompt: str,
):
    """Two-phase executor that forces tool use on the first model call.

    Yields LangChain messages as they accumulate, mimicking the agent stream
    interface so the evaluator display code works unchanged.
    """
    from langchain_core.messages import (
        SystemMessage, HumanMessage, AIMessage, ToolMessage
    )

    tool_map  = {t.name: t for t in tools}
    messages  = [SystemMessage(content=system), HumanMessage(content=prompt)]
    all_msgs  = list(messages)

    # ── Turn 1: force the model to call a tool ─────────────────────────────
    # tool_choice="any" means "pick any tool" — not a specific one.
    # This works with Gemini, Claude, and OpenAI via LangChain.
    model_forced = model.bind_tools(tools, tool_choice="any")
    ai_msg = await model_forced.ainvoke(messages)
    all_msgs.append(ai_msg)
    yield all_msgs[:]   # let the display update

    # ── Execute every tool call the model made ─────────────────────────────
    tool_calls = getattr(ai_msg, "tool_calls", []) or []
    for tc in tool_calls:
        tool_name = tc.get("name", "")
        tool_args = tc.get("args", {})
        tool_id   = tc.get("id", tool_name)
        tool_fn   = tool_map.get(tool_name)
        if tool_fn is None:
            content = f"ERROR: unknown tool '{tool_name}'"
        else:
            try:
                content = await tool_fn.ainvoke(tool_args)
            except Exception as exc:
                content = f"ERROR: {exc}"
        tool_msg = ToolMessage(content=str(content), tool_call_id=tool_id, name=tool_name)
        all_msgs.append(tool_msg)
        yield all_msgs[:]

    # ── Turn 2+: open-ended react loop for further tool calls / final answer ─
    model_free = model.bind_tools(tools)
    while True:
        ai_msg = await model_free.ainvoke(all_msgs)
        all_msgs.append(ai_msg)
        yield all_msgs[:]

        tool_calls = getattr(ai_msg, "tool_calls", []) or []
        if not tool_calls:
            break   # model gave a final text answer — done

        for tc in tool_calls:
            tool_name = tc.get("name", "")
            tool_args = tc.get("args", {})
            tool_id   = tc.get("id", tool_name)
            tool_fn   = tool_map.get(tool_name)
            if tool_fn is None:
                content = f"ERROR: unknown tool '{tool_name}'"
            else:
                try:
                    content = await tool_fn.ainvoke(tool_args)
                except Exception as exc:
                    content = f"ERROR: {exc}"
            tool_msg = ToolMessage(content=str(content), tool_call_id=tool_id, name=tool_name)
            all_msgs.append(tool_msg)
            yield all_msgs[:]


# ── Agent factories — return (executor_fn, system_prompt) ─────────────────────

async def create_parcs_agent(model_name: str | None = None) -> tuple[Any, str]:
    model = get_model(model_name)
    tools = await get_parcs_mcp_tools()

    skill_path = config.paths.skills_dir / "parcs-cluster" / "SKILL.md"
    skill_text = skill_path.read_text(encoding="utf-8") if skill_path.exists() else ""

    system = _PARALLEL_SYSTEM
    if skill_text:
        system += f"\n\n---\n\nPARCS SKILL REFERENCE:\n{skill_text}"

    # Return a bound coroutine so the caller just does `agent(prompt)` -> async gen
    async def _executor(prompt: str):
        async for msgs in forced_tool_astream(model, tools, system, prompt):
            yield msgs

    return _executor, system


def create_sequential_agent(model_name: str | None = None) -> tuple[Any, str]:
    model = get_model(model_name)

    async def _executor(prompt: str):
        async for msgs in forced_tool_astream(model, [python_exec], _SEQUENTIAL_SYSTEM, prompt):
            yield msgs

    return _executor, _SEQUENTIAL_SYSTEM
