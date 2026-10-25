from __future__ import annotations

from collections.abc import Callable

from langchain_core.language_models import BaseChatModel

from .settings import LLMSettings, Provider


def _google_model_id(name: str) -> str:
    return name.removeprefix("google/").removeprefix("publishers/google/models/")


def _openai(s: LLMSettings, name: str) -> BaseChatModel:
    from langchain_openai import ChatOpenAI

    return ChatOpenAI(
        model=name,
        temperature=s.temperature,
        base_url=s.base_url,
        api_key=s.api_key,  # type: ignore
        stream_usage=True,
        streaming=True,
    )


def _vertexai(s: LLMSettings, name: str) -> BaseChatModel:
    from langchain_google_vertexai import ChatGoogleGenerativeAI  # type: ignore

    return ChatGoogleGenerativeAI(
        model_name=_google_model_id(name),
        project=s.project or None,
        location=s.location,
        temperature=s.temperature,
        streaming=True,
    )


def _google(s: LLMSettings, name: str) -> BaseChatModel:
    from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore

    return ChatGoogleGenerativeAI(
        model=_google_model_id(name),
        temperature=s.temperature,
        google_api_key=s.api_key or None,
    )


def _anthropic(s: LLMSettings, name: str) -> BaseChatModel:
    from langchain_anthropic import ChatAnthropic  # type: ignore

    return ChatAnthropic(
        model_name=name,
        temperature=s.temperature,
        api_key=s.api_key or None,  # type: ignore
        streaming=True,
    )


# Provider packages are imported inside the builders, so only the one in use has to be installed.
_BUILDERS: dict[Provider, Callable[[LLMSettings, str], BaseChatModel]] = {
    Provider.OPENAI: _openai,
    Provider.VERTEXAI: _vertexai,
    Provider.GOOGLE: _google,
    Provider.ANTHROPIC: _anthropic,
}


def create_model(settings: LLMSettings, model_name: str | None = None) -> BaseChatModel:
    """The chat model for ``settings.provider``; ``model_name`` overrides ``settings.model_name``."""
    return _BUILDERS[settings.provider](settings, model_name or settings.model_name)
