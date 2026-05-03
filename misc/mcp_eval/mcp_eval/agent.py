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
    import inspect

    sig    = inspect.signature(create_react_agent)
    params = set(sig.parameters)

    # system message is injected per-call via input messages — don't pass it here
    return create_react_agent(model=model, tools=tools)


# ── Agent factories — return (agent, system_prompt) ───────────────────────────

async def create_parcs_agent(model_name: str | None = None) -> tuple[Any, str]:
    model = get_model(model_name)
    tools = await get_parcs_mcp_tools()

    skill_path = config.paths.skills_dir / "parcs-cluster" / "SKILL.md"
    skill_text = skill_path.read_text(encoding="utf-8") if skill_path.exists() else ""

    system = _PARALLEL_SYSTEM
    if skill_text:
        system += f"\n\n---\n\nPARCS SKILL REFERENCE:\n{skill_text}"

    return _make_react_agent(model, tools), system


def create_sequential_agent(model_name: str | None = None) -> tuple[Any, str]:
    model = get_model(model_name)
    return _make_react_agent(model, [python_exec]), _SEQUENTIAL_SYSTEM
