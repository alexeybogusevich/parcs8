"""Model specs: the OpenRouter provider pin and reasoning effort reach the request and the manifest."""

import json

from eval_utils import LLMSettings, Provider
from langchain_core.messages import HumanMessage

from tool_calling_vs_ptc import agent as agent_mod
from tool_calling_vs_ptc.analysis import pricing
from tool_calling_vs_ptc.analysis.cli import pinned
from tool_calling_vs_ptc.config import ModelSpec

BASE = LLMSettings(provider=Provider.OPENAI, base_url="https://openrouter.ai/api/v1", api_key="k", model_name="x")
PINNED = ModelSpec(id="m", model_name="vendor/m", openrouter_provider="vendor/fp8", reasoning_effort="low")


def test_pin_and_effort_go_to_the_request_body_not_to_the_llm_settings():
    assert PINNED.extra_body() == {
        "provider": {"order": ["vendor/fp8"], "allow_fallbacks": False},
        "reasoning": {"effort": "low"},
    }
    assert ModelSpec(id="m", model_name="vendor/m").extra_body() == {}
    assert PINNED.llm(BASE).model_name == "vendor/m"


def test_manifest_records_the_pin_and_the_effort():
    public = PINNED.public(BASE)
    assert public["openrouter_provider"] == "vendor/fp8"
    assert public["reasoning_effort"] == "low"
    assert ModelSpec(id="m", model_name="vendor/m").public(BASE)["reasoning_effort"] == "default"


async def test_the_agent_model_sends_the_extra_body(monkeypatch):
    seen = {}

    async def no_tools(url):
        return []

    def capture(model, arm, tools, ptc_options):
        seen["model"] = model

    monkeypatch.setattr(agent_mod, "parcs_tools", no_tools)
    monkeypatch.setattr(agent_mod, "build_agent", capture)
    await agent_mod.create_run_agent("http://x", PINNED.llm(BASE), "ptc", {}, PINNED.extra_body())
    payload = seen["model"]._get_request_payload([HumanMessage("hi")])
    assert payload["extra_body"] == PINNED.extra_body()


def test_pinned_models_are_priced_at_their_endpoint(tmp_path, monkeypatch):
    (tmp_path / "manifest.json").write_text(
        json.dumps({"hash": "h", "manifest": {"models": [PINNED.public(BASE)]}}), encoding="utf-8"
    )
    endpoints = {"vendor/fp8": {"prompt": "2", "completion": "3"}, "vendor": {"prompt": "1", "completion": "1"}}

    class Response:
        def __init__(self, data):
            self.data = data

        def json(self):
            return {"data": self.data}

    def get(url, timeout):
        if url.endswith("/endpoints"):
            return Response({"endpoints": [{"tag": t, "pricing": p} for t, p in endpoints.items()]})
        return Response([{"id": "vendor/m", "pricing": {"prompt": "9", "completion": "9"}}])

    monkeypatch.setattr(pricing.httpx, "get", get)
    path = tmp_path / "pricing.json"
    prices = pricing.snapshot(path, ["vendor/m"], endpoints=pinned(tmp_path))
    assert prices["models"]["vendor/m"] == {"prompt": "2", "completion": "3", "endpoint": "vendor/fp8"}
    # An unpinned snapshot of the same model is stale once the model is pinned.
    path.write_text(json.dumps({"models": {"vendor/m": {"prompt": "9", "completion": "9"}}}), encoding="utf-8")
    assert pricing.snapshot(path, ["vendor/m"], endpoints=pinned(tmp_path))["models"]["vendor/m"]["prompt"] == "2"
