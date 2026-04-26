import os
from pathlib import Path

from dotenv import load_dotenv
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


_PACKAGE_ROOT = Path(__file__).resolve().parent.parent
_ENV_FILE = _PACKAGE_ROOT / ".env"

load_dotenv(_ENV_FILE)  # makes unmodeled vars (e.g. HF_TOKEN) visible to libs that read os.environ


class LLMSettings(BaseModel):
    model_name: str = "qwen/qwen3.6-27b"
    temperature: float = 0.6
    base_url: str = "http://localhost:1234/v1"
    api_key: str = "lmstudio"


class MCPSettings(BaseModel):
    cluster_url: str


class PathSettings(BaseModel):
    skills_dir: Path = _PACKAGE_ROOT / "skills"
    memory_dir: Path = _PACKAGE_ROOT / "memory"


class LangSmithSettings(BaseModel):
    tracing_v2: bool = False
    endpoint: str = "https://api.smith.langchain.com"
    project: str | None = None
    api_key: str | None = None

    def apply_to_environ(self) -> None:
        """Export settings as LANGCHAIN_* env vars consumed by the LangSmith SDK."""
        os.environ["LANGCHAIN_TRACING_V2"] = "true" if self.tracing_v2 else "false"
        os.environ["LANGCHAIN_ENDPOINT"] = self.endpoint
        if self.project:
            os.environ["LANGCHAIN_PROJECT"] = self.project
        if self.api_key:
            os.environ["LANGCHAIN_API_KEY"] = self.api_key


class Config(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=_ENV_FILE,
        env_file_encoding="utf-8",
        env_nested_delimiter="__",
        extra="ignore",
    )

    llm: LLMSettings = Field(default_factory=LLMSettings)
    mcp: MCPSettings
    paths: PathSettings = Field(default_factory=PathSettings)
    langsmith: LangSmithSettings = Field(default_factory=LangSmithSettings)


config = Config()
config.langsmith.apply_to_environ()
