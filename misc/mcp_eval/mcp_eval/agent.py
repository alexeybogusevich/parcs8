from deepagents import create_deep_agent
from langchain_openai import ChatOpenAI
from deepagents.backends import CompositeBackend, StateBackend, FilesystemBackend

from .tools import get_parcs_mcp_tools
from .config import config


def get_model() -> ChatOpenAI:
    """Creates and returns a ChatOpenAI model instance based on the configuration."""
    return ChatOpenAI(
        model=config.llm.model_name,
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
    )
