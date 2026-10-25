from pathlib import Path

from dotenv import load_dotenv
from eval_utils import EvalSettings
from parcs_agent import Mode
from pydantic import BaseModel, Field
from pydantic_settings import SettingsConfigDict

_ENV_FILE = Path(__file__).resolve().parent.parent / ".env"

load_dotenv(_ENV_FILE)


class AgentSettings(BaseModel):
    # "tool_calling": the model calls the PARCS tools turn by turn.
    # "ptc": the model writes JavaScript that calls them as tools.* in QuickJS.
    mode: Mode = "tool_calling"
    # QuickJS VM time per eval; time spent awaiting tools.* calls is not counted.
    eval_timeout_seconds: float = 30.0
    max_ptc_calls: int = 256
    # Cap on what one eval result returns to the model.
    max_result_chars: int = 4000


class Config(EvalSettings):
    model_config = SettingsConfigDict(env_file=_ENV_FILE)

    agent: AgentSettings = Field(default_factory=AgentSettings)


config = Config()  # type: ignore
config.langsmith.apply_to_environ()
