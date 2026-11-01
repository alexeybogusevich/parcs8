from __future__ import annotations

from eval_utils import ThreadNonceMiddleware, create_model
from langchain.agents import create_agent
from langchain.agents.middleware import ToolRetryMiddleware
from parcs_agent import Mode, create_parcs_middleware

from .config import config


async def create_parcs_agent(mode: Mode | None = None):
    """An agent with the PARCS tools of the configured cluster, called directly or from code (PTC)."""
    mode = mode or config.agent.mode
    options = (
        {
            "timeout": config.agent.eval_timeout_seconds,
            "max_ptc_calls": config.agent.max_ptc_calls,
            "max_result_chars": config.agent.max_result_chars,
        }
        if mode == "ptc"
        else {}
    )
    parcs = await create_parcs_middleware(
        mode,
        {"transport": "streamable_http", "url": config.mcp.cluster_url.rstrip("/") + "/"},
        **options,
    )
    return create_agent(
        name="ParcsAgent",
        model=create_model(config.llm),
        middleware=[
            ToolRetryMiddleware(max_retries=3, backoff_factor=2.0, initial_delay=1.0),
            ThreadNonceMiddleware(),
            parcs,
        ],
    )
