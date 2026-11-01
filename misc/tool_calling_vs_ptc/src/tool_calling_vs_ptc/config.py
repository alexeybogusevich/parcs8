"""Settings: the shared ``EvalSettings`` plus an ``experiment`` section (``EXPERIMENT__*`` in ``.env``)."""

from __future__ import annotations

from pathlib import Path

from dotenv import load_dotenv
from eval_utils import EvalSettings, LLMSettings, Provider
from parcs_agent import Mode
from pydantic import BaseModel, Field
from pydantic_settings import SettingsConfigDict

from .tasks import N_GRID

ROOT = Path(__file__).resolve().parents[2]
ENV_FILE = ROOT / ".env"
DEFAULT_LANGSMITH_PROJECT = "parcs-tc-vs-ptc"


class ModelSpec(BaseModel):
    """One model of the sweep. ``id`` names it in run ids; unset fields fall back to ``LLM__*``."""

    id: str
    model_name: str
    provider: Provider | None = None
    base_url: str | None = None
    api_key: str | None = None
    temperature: float | None = None
    # OpenRouter only: the endpoint tag every call is pinned to (no fallbacks), e.g. "openai" or
    # "deepinfra/fp8", so that one host, quantization and price serve the whole sweep.
    openrouter_provider: str | None = None
    # None leaves the provider's default effort; either way it is recorded in the manifest.
    reasoning_effort: str | None = None

    def llm(self, base: LLMSettings) -> LLMSettings:
        overrides = self.model_dump(exclude={"id", "openrouter_provider", "reasoning_effort"}, exclude_none=True)
        return base.model_copy(update=overrides)

    def extra_body(self) -> dict:
        """Request fields beyond the OpenAI API: OpenRouter's provider pin and reasoning effort."""
        body: dict = {}
        if self.openrouter_provider:
            body["provider"] = {"order": [self.openrouter_provider], "allow_fallbacks": False}
        if self.reasoning_effort:
            body["reasoning"] = {"effort": self.reasoning_effort}
        return body

    def public(self, base: LLMSettings) -> dict:
        """What identifies the model in the manifest (no secrets)."""
        llm = self.llm(base)
        return {
            "id": self.id,
            "provider": str(llm.provider),
            "model_name": llm.model_name,
            "temperature": llm.temperature,
            "base_url": llm.base_url if llm.provider == Provider.OPENAI else None,
            "openrouter_provider": self.openrouter_provider,
            "reasoning_effort": self.reasoning_effort or "default",
        }


class ExperimentSettings(BaseModel):
    models: list[ModelSpec] = Field(default_factory=list)
    families: list[str] = Field(default_factory=lambda: ["f1", "f2"])
    n_grid: list[int] = Field(default_factory=lambda: list(N_GRID))
    arms: list[Mode] = Field(default_factory=lambda: ["tool_calling", "ptc"])
    reps: int = 5
    seed: int = 20261025
    # LangGraph steps per run, a runaway guard only (a tool-calling turn is about 2 steps): tool calling
    # at F2 N = 32 needs ~72 steps per attempt, and a recovery reruns the plan. The wall budget binds.
    recursion_limit: int = 600
    # The only wall-clock limit on a run (the PTC eval timeout counts QuickJS VM time only).
    wall_budget_seconds: float = 3600.0
    # ptc only
    eval_timeout_seconds: float = 30.0
    max_ptc_calls: int = 256
    max_result_chars: int = 4000
    # Before each run, wait this long at most for the cluster to have no running or queued layers.
    idle_timeout_seconds: float = 600.0
    results_dir: Path = ROOT / "results" / "sweep"

    def ptc_options(self) -> dict:
        return {
            "timeout": self.eval_timeout_seconds,
            "max_ptc_calls": self.max_ptc_calls,
            "max_result_chars": self.max_result_chars,
        }


class Config(EvalSettings):
    model_config = SettingsConfigDict(env_file=ENV_FILE)

    experiment: ExperimentSettings = Field(default_factory=ExperimentSettings)

    def models(self) -> list[ModelSpec]:
        """The configured models, or the ``LLM__*`` model alone."""
        if self.experiment.models:
            return self.experiment.models
        name = self.llm.model_name
        return [ModelSpec(id=name.rsplit("/", 1)[-1], model_name=name)]


def load_config(**overrides) -> Config:
    load_dotenv(ENV_FILE)
    config = Config(**overrides)  # type: ignore[call-arg]
    if not config.langsmith.project:
        config.langsmith.project = DEFAULT_LANGSMITH_PROJECT
    config.langsmith.apply_to_environ()
    return config
