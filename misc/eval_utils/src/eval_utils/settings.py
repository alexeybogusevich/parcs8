"""Settings shared by the evaluation harnesses: the LLM, the PARCS MCP server and LangSmith tracing.

Fields are read from the environment (and an optional ``.env``) with ``__`` as the nested delimiter,
e.g. ``LLM__PROVIDER``, ``MCP__CLUSTER_URL``, ``LANGSMITH__API_KEY``.
"""

from __future__ import annotations

import os
from enum import StrEnum

from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Provider(StrEnum):
    OPENAI = "openai"  # any OpenAI-compatible endpoint (local / OpenRouter)
    VERTEXAI = "vertexai"
    GOOGLE = "google"
    ANTHROPIC = "anthropic"


class LLMSettings(BaseModel):
    provider: Provider = Provider.OPENAI
    model_name: str = "qwen/qwen3.6-27b"
    temperature: float = 0.0
    # OpenAI-compatible (local / OpenRouter)
    base_url: str = "http://localhost:1234/v1"
    # API key for the openai, google and anthropic providers
    api_key: str = "lmstudio"
    # Vertex AI
    project: str = ""
    location: str = "us-central1"


class MCPSettings(BaseModel):
    # Base URL of the PARCS MCP server (Streamable HTTP), e.g. http://34.116.216.29:8080
    cluster_url: str


class LangSmithSettings(BaseModel):
    tracing_v2: bool = False
    endpoint: str = "https://api.smith.langchain.com"
    project: str | None = None
    api_key: str | None = None

    def apply_to_environ(self) -> None:
        if not self.tracing_v2:
            # Hard-disable: unset every env var the LangSmith SDK checks
            for key in (
                "LANGCHAIN_TRACING_V2",
                "LANGCHAIN_API_KEY",
                "LANGCHAIN_ENDPOINT",
                "LANGCHAIN_PROJECT",
                "LANGSMITH_TRACING",
                "LANGSMITH_API_KEY",
            ):
                os.environ.pop(key, None)
            os.environ["LANGCHAIN_TRACING_V2"] = "false"
            return
        os.environ["LANGCHAIN_TRACING_V2"] = "true"
        os.environ["LANGCHAIN_ENDPOINT"] = self.endpoint
        if self.project:
            os.environ["LANGCHAIN_PROJECT"] = self.project
        if self.api_key:
            os.environ["LANGCHAIN_API_KEY"] = self.api_key


class EvalSettings(BaseSettings):
    """Base for a harness's settings: subclass it to add harness-specific sections and point
    ``model_config`` at the harness's own ``.env``."""

    model_config = SettingsConfigDict(env_file_encoding="utf-8", env_nested_delimiter="__", extra="ignore")

    llm: LLMSettings = Field(default_factory=LLMSettings)
    mcp: MCPSettings
    langsmith: LangSmithSettings = Field(default_factory=LangSmithSettings)
