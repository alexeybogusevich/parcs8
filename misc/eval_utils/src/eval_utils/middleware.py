from __future__ import annotations

from uuid import uuid4

from langchain.agents.middleware import AgentMiddleware
from langchain_core.messages import SystemMessage


class ThreadNonceMiddleware(AgentMiddleware):
    """Starts the system prompt with a per-thread nonce, so runs never share a provider prompt-cache prefix.

    Turns within one thread keep the same nonce and still reuse each other's cache.
    """

    def __init__(self) -> None:
        super().__init__()
        self._nonces: dict[str | None, str] = {}

    def _with_nonce(self, request):
        info = getattr(request.runtime, "execution_info", None)
        nonce = self._nonces.setdefault(getattr(info, "thread_id", None), uuid4().hex)
        blocks = list(request.system_message.content_blocks) if request.system_message else []
        return request.override(
            system_message=SystemMessage(content_blocks=[{"type": "text", "text": f"Run nonce: {nonce}\n\n"}, *blocks])
        )

    def wrap_model_call(self, request, handler):
        return handler(self._with_nonce(request))

    async def awrap_model_call(self, request, handler):
        return await handler(self._with_nonce(request))
