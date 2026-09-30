import os
from pathlib import Path

from dotenv import load_dotenv
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


_PACKAGE_ROOT = Path(__file__).resolve().parent.parent
_ENV_FILE = _PACKAGE_ROOT / ".env"

load_dotenv(_ENV_FILE)


class LLMSettings(BaseModel):
    provider: str = "openai"  # "openai" | "vertexai"
    model_name: str = "qwen/qwen3.6-27b"
    temperature: float = 0.0
    # OpenAI-compatible (local / OpenRouter)
    base_url: str = "http://localhost:1234/v1"
    api_key: str = "lmstudio"
    # Vertex AI
    project: str = ""
    location: str = "us-central1"


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


class EvalSettings(BaseModel):
    # Comma-separated Vertex AI model names to evaluate
    models: str = "gemini-2.5-flash,gemini-2.5-pro"
    # Where to write/append results
    results_file: Path = _PACKAGE_ROOT / "results" / "benchmark_results.csv"
    # Max time (seconds) we wait for a single agent run before giving up
    timeout_seconds: int = 900
    # Which task IDs to run (empty = all 15)
    task_ids: str = ""


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
    eval: EvalSettings = Field(default_factory=EvalSettings)


config = Config() # type: ignore
config.langsmith.apply_to_environ()
