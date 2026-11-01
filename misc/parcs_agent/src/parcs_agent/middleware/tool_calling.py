from __future__ import annotations

from collections.abc import Awaitable, Callable, Sequence
from typing import Any

from langchain.agents.middleware import AgentMiddleware, ModelRequest, ModelResponse
from langchain_core.tools import BaseTool

from ..prompts import SHARED, TOOL_CALLING
from ._guidance import GuidanceMixin


class ParcsToolCallingMiddleware(GuidanceMixin, AgentMiddleware):
    """Gives the agent the PARCS tools as ordinary tool calls, plus guidance on using them.

    Works with any harness that accepts LangChain middleware (``create_agent``, deepagents, ...).
    ``guidance`` replaces the built-in system-prompt text; ``""`` adds none.
    """

    def __init__(self, tools: Sequence[BaseTool], *, guidance: str | None = None) -> None:
        super().__init__()
        self.tools = list(tools)
        self._guidance = f"{SHARED}\n{TOOL_CALLING}" if guidance is None else guidance

    def wrap_model_call(
        self, request: ModelRequest[Any], handler: Callable[[ModelRequest[Any]], ModelResponse[Any]]
    ) -> ModelResponse[Any]:
        return handler(self._with_guidance(request))

    async def awrap_model_call(
        self, request: ModelRequest[Any], handler: Callable[[ModelRequest[Any]], Awaitable[ModelResponse[Any]]]
    ) -> ModelResponse[Any]:
        return await handler(self._with_guidance(request))
