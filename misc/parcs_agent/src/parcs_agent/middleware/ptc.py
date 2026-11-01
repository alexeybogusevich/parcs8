from __future__ import annotations

from collections.abc import Awaitable, Callable, Sequence
from typing import Any

from langchain.agents.middleware import ModelRequest, ModelResponse
from langchain_core.tools import BaseTool
from langchain_quickjs import CodeInterpreterMiddleware

from ..prompts import PTC, SHARED
from ._guidance import GuidanceMixin


class ParcsPTCMiddleware(GuidanceMixin, CodeInterpreterMiddleware):
    """Programmatic tool calling: the PARCS tools are called from code, not by the model directly.

    A QuickJS ``eval`` tool is added to the agent and the PARCS tools are bound inside it as
    ``tools.getClusterInfo(...)``, ``tools.runLayer(...)``, ... each resolving to the MCP content
    blocks (the server envelope is ``JSON.parse(r[0].text)``). The PARCS tools are *not* offered as
    direct tool calls.

    ``interpreter_options`` go to :class:`CodeInterpreterMiddleware` (``timeout``, ``max_ptc_calls``,
    ``max_result_chars``, ``mode``, ``memory_limit``, ...). ``guidance`` replaces the built-in
    system-prompt text; ``""`` adds none.
    """

    def __init__(self, tools: Sequence[BaseTool], *, guidance: str | None = None, **interpreter_options: Any) -> None:
        interpreter_options.setdefault("subagents", False)
        super().__init__(ptc=list(tools), **interpreter_options)
        self._guidance = f"{SHARED}\n{PTC.format(eval_tool=self._tool_name)}" if guidance is None else guidance

    def wrap_model_call(
        self, request: ModelRequest[Any], handler: Callable[[ModelRequest[Any]], ModelResponse[Any]]
    ) -> ModelResponse[Any]:
        return super().wrap_model_call(self._with_guidance(request), handler)

    async def awrap_model_call(
        self, request: ModelRequest[Any], handler: Callable[[ModelRequest[Any]], Awaitable[ModelResponse[Any]]]
    ) -> ModelResponse[Any]:
        return await super().awrap_model_call(self._with_guidance(request), handler)
