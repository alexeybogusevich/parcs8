from __future__ import annotations

from typing import Any

from langchain.agents.middleware import ModelRequest
from langchain_core.messages import SystemMessage


def append_to_system_message(message: SystemMessage | None, text: str) -> SystemMessage:
    """``message`` with ``text`` added as a trailing block (or a new message if there is none)."""
    blocks = list(message.content_blocks) if message else []
    sep = "\n\n" if blocks else ""
    return SystemMessage(content_blocks=[*blocks, {"type": "text", "text": f"{sep}{text}"}])


class GuidanceMixin:
    """Adds ``guidance`` to the system prompt of every model request."""

    _guidance: str

    def _with_guidance(self, request: ModelRequest[Any]) -> ModelRequest[Any]:
        if not self._guidance:
            return request
        return request.override(system_message=append_to_system_message(request.system_message, self._guidance))
