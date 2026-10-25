import pytest
from langchain_openai import ChatOpenAI

from eval_utils import LLMSettings, Provider, create_model
from eval_utils.models import _BUILDERS, _google_model_id


def test_every_provider_has_a_builder():
    assert set(_BUILDERS) == set(Provider)


def test_openai_provider_uses_settings():
    model = create_model(LLMSettings(model_name="m", base_url="http://x/v1", api_key="k", temperature=0.3))
    assert isinstance(model, ChatOpenAI)
    assert model.model_name == "m"
    assert model.temperature == 0.3


def test_model_name_override():
    assert create_model(LLMSettings(api_key="k"), "other").model_name == "other"  # type: ignore[attr-defined]


def test_unknown_provider_rejected():
    with pytest.raises(ValueError):
        LLMSettings(provider="nope")  # type: ignore[arg-type]


def test_google_prefixes_stripped():
    assert _google_model_id("publishers/google/models/gemini-x") == "gemini-x"
    assert _google_model_id("google/gemini-x") == "gemini-x"
