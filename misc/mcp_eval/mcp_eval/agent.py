from __future__ import annotations

import os

from langchain_core.language_models import BaseChatModel
from deepagents import create_deep_agent
from deepagents.backends import CompositeBackend, StateBackend, FilesystemBackend

from .python_exec import python_exec
from .tools import get_parcs_mcp_tools
from .config import config


def get_model(model_name: str | None = None) -> BaseChatModel:
    """Return a chat model based on the configured provider."""
    name = model_name or config.llm.model_name

    # Strip any Model Garden resource-ID prefix (e.g. "google/gemini-2.0-flash-001")
    clean_name = name.removeprefix("google/").removeprefix("publishers/google/models/")

    if config.llm.provider == "vertexai":
        # The new google-genai SDK uses vertexai=True to route through
        # the modern Vertex AI endpoint (not the old aiplatform path).
        # We set GOOGLE_API_KEY to a sentinel so pydantic validation passes,
        # but the pre-built client takes over for all actual calls.
        from google import genai as _ggenai  # type: ignore
        from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore

        vertex_client = _ggenai.Client(
            vertexai=True,
            project=config.llm.project or None,
            location=config.llm.location,
        )
        # Sentinel bypasses the "API key required" validator; the client
        # object overrides it for real calls.
        os.environ.setdefault("GOOGLE_API_KEY", "_vertex_adc_")
        return ChatGoogleGenerativeAI(
            model=clean_name,
            temperature=config.llm.temperature,
            client=vertex_client,
        )

    if config.llm.provider == "google":
        # Google AI Studio API key
        from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore

        return ChatGoogleGenerativeAI(
            model=clean_name,
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

    # Default: OpenAI-compatible endpoint
    from langchain_openai import ChatOpenAI

    return ChatOpenAI(
        model=name,
        temperature=config.llm.temperature,
        base_url=config.llm.base_url,
        api_key=config.llm.api_key,  # type: ignore
        stream_usage=True,
        streaming=True,
    )


def _make_backend() -> CompositeBackend:
    return CompositeBackend(
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


async def create_parcs_agent(model_name: str | None = None):
    """Parallel agent: has PARCS MCP tools, no Python REPL."""
    model = get_model(model_name)
    backend = _make_backend()
    tools = await get_parcs_mcp_tools()

    return create_deep_agent(
        name="ParcsAgent",
        model=model,
        tools=tools,
        backend=backend,
        skills=["/skills/"],
        memory=["/memory/AGENTS.md"],
    )


def create_sequential_agent(model_name: str | None = None):
    """Sequential agent: Python REPL only, zero knowledge of PARCS."""
    model = get_model(model_name)
    backend = _make_backend()

    return create_deep_agent(
        name="SequentialAgent",
        model=model,
        tools=[python_exec],
        backend=backend,
        skills=[],   # no PARCS skill
        memory=["/memory/SEQUENTIAL.md"],
    )
