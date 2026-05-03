from __future__ import annotations


from langchain_core.language_models import BaseChatModel

from .tools import get_parcs_mcp_tools
from .config import config

from deepagents import create_deep_agent
from langchain_openai import ChatOpenAI
from deepagents.backends import CompositeBackend, StateBackend, FilesystemBackend
from langchain.agents.middleware import ToolRetryMiddleware

def get_model(model_name: str | None = None) -> BaseChatModel:
    name = model_name or config.llm.model_name
    clean = name.removeprefix("google/").removeprefix("publishers/google/models/")

    if config.llm.provider == "vertexai":
        from langchain_google_vertexai import ChatGoogleGenerativeAI  # type: ignore

        return ChatGoogleGenerativeAI(
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

    return ChatOpenAI(
        model=name,
        temperature=config.llm.temperature,
        base_url=config.llm.base_url,
        api_key=config.llm.api_key,  # type: ignore
        stream_usage=True,
        streaming=True,
    )

async def create_parcs_agent():
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

    tools = await get_parcs_mcp_tools()

    return create_deep_agent(
        name="ParcsAgent",
        model=model,
        tools=tools,
        backend=backend,
        skills=["/skills/"],
        memory=["/memory/AGENTS.md"],
        middleware=[
            ToolRetryMiddleware(
                max_retries=3,
                backoff_factor=2.0,
                initial_delay=1.0,
            ),
        ],
    )
