from __future__ import annotations


from langchain_core.language_models import BaseChatModel

from .tools import CallLog, code_variants, get_parcs_mcp_tools
from .config import config

from deepagents import create_deep_agent
from langchain_openai import ChatOpenAI
from deepagents.backends import CompositeBackend, StateBackend, FilesystemBackend
from langchain.agents.middleware import ToolRetryMiddleware

def get_model(model_name: str | None = None) -> BaseChatModel:
    name = model_name or config.llm.model_name
    clean = name.removeprefix("google/").removeprefix("publishers/google/models/")

    if config.llm.provider == "vertexai":
        # langchain-google-vertexai 3.x only ships ChatVertexAI; the genai client reaches Vertex AI too.
        from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore

        return ChatGoogleGenerativeAI(
            model=clean,
            vertexai=True,
            project=config.llm.project or None,
            location=config.llm.location,
            temperature=config.llm.temperature,
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

    return ChatOpenAI(
        model=name,
        temperature=config.llm.temperature,
        base_url=config.llm.base_url,
        api_key=config.llm.api_key,  # type: ignore
        stream_usage=True,
        streaming=True,
    )

async def create_parcs_agent(orchestration: str | None = None, log: CallLog | None = None):
    """Build the PARCS agent for one orchestration arm.

    direct — the model calls the PARCS tools itself, one tool call per step (arm C).
    ptc    — the model gets the QuickJS `eval` tool and calls the PARCS tools from code as
             `tools.runLayer({...})` etc., each returning the server envelope as a JS object
             (arm D). Same tools, descriptions and result shape; only the caller differs.
    """
    orchestration = (orchestration or config.ptc.orchestration).lower()
    if orchestration not in ("direct", "ptc"):
        raise ValueError(f"orchestration must be 'direct' or 'ptc', not {orchestration!r}")

    model = get_model()
    backend = CompositeBackend(
        default=StateBackend(),
        routes={
            "/skills/": FilesystemBackend(
                root_dir=str(config.paths.skills_dir), virtual_mode=True
            ),
            "/memory/": FilesystemBackend(
                root_dir=str(config.paths.memory_dir), virtual_mode=True
            ),
        },
    )

    parcs_tools = await get_parcs_mcp_tools(log)
    middleware = [
        ToolRetryMiddleware(
            max_retries=3,
            backoff_factor=2.0,
            initial_delay=1.0,
        ),
    ]

    if orchestration == "ptc":
        from langchain_quickjs import CodeInterpreterMiddleware

        middleware.append(
            CodeInterpreterMiddleware(
                ptc=list(code_variants(parcs_tools)),
                timeout=config.ptc.eval_timeout_seconds,
                max_ptc_calls=config.ptc.max_ptc_calls,
                max_result_chars=config.ptc.max_result_chars,
                mode=config.ptc.mode,
                subagents=False,
            )
        )
        tools = parcs_tools if config.ptc.expose_direct_tools else []
    else:
        tools = parcs_tools

    return create_deep_agent(
        name="ParcsAgent",
        model=model,
        tools=tools,
        backend=backend,
        skills=["/skills/"],
        memory=["/memory/AGENTS.md"],
        middleware=middleware,
    )
