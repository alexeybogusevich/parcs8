"""Cost per model call from token counts and the provider's per-token prices, cache tiers included.

The streaming client keeps no provider generation id, so billed amounts cannot be looked up per call;
the cost is computed instead, the way LangSmith's cache-aware price map does (checked in 4.3):
uncached input at the prompt price, cache reads and cache writes at their own prices, output (with
reasoning tokens) at the completion price, and the long-context tier when a call's prompt is large
enough. Prices come from OpenRouter's public model list, snapshotted once per results directory
(``pricing.json``) so that costs do not move when the list changes. A model pinned to one provider
(``openrouter_provider`` in the manifest) is priced at that endpoint instead, since endpoints of the
same model differ in price.
"""

from __future__ import annotations

import json
import time
from pathlib import Path

import httpx

OPENROUTER_MODELS = "https://openrouter.ai/api/v1/models"


def endpoint_pricing(model_name: str, tag: str) -> dict:
    """The pricing of the endpoint ``tag`` of a model, marked with that tag."""
    data = httpx.get(f"{OPENROUTER_MODELS}/{model_name}/endpoints", timeout=60).json()["data"]
    for endpoint in data["endpoints"]:
        if endpoint.get("tag") == tag:
            return {**endpoint["pricing"], "endpoint": tag}
    raise LookupError(f"{model_name} has no OpenRouter endpoint {tag!r}")


def fetch(model_names: list[str], endpoints: dict[str, str | None] | None = None) -> dict:
    data = httpx.get(OPENROUTER_MODELS, timeout=60).json()["data"]
    prices = {m["id"]: m.get("pricing") or {} for m in data if m["id"] in set(model_names)}
    for name, tag in (endpoints or {}).items():
        if tag and name in model_names:
            prices[name] = endpoint_pricing(name, tag)
    return {"source": OPENROUTER_MODELS, "fetched_at": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "models": prices}


def snapshot(
    path: Path, model_names: list[str], refresh: bool = False, endpoints: dict[str, str | None] | None = None
) -> dict:
    """The stored snapshot, fetched (or extended with missing models) when needed.

    ``endpoints`` maps a model to the provider tag it was pinned to; a stored price for another
    endpoint counts as missing.
    """
    endpoints = endpoints or {}
    stored = json.loads(path.read_text(encoding="utf-8")) if path.exists() and not refresh else None
    missing = [
        m
        for m in model_names
        if stored is None or m not in stored["models"] or stored["models"][m].get("endpoint") != endpoints.get(m)
    ]
    if missing:
        fresh = fetch(missing, endpoints)
        stored = fresh if stored is None else {**stored, "models": {**stored["models"], **fresh["models"]}}
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(stored, indent=2) + "\n", encoding="utf-8")
    return stored or {"models": {}}


def _tier(pricing: dict, input_tokens: int) -> dict:
    tier = dict(pricing)
    for override in pricing.get("overrides") or []:
        if input_tokens >= override.get("min_prompt_tokens", float("inf")):
            tier.update(override)
    return tier


def call_cost(usage: dict, pricing: dict | None, cached: bool = True) -> float | None:
    """USD for one call; ``cached=False`` prices every input token as uncached (the no-cache counterfactual)."""
    if not pricing or "prompt" not in pricing:
        return None
    inp = usage.get("input_tokens") or 0
    out = usage.get("output_tokens") or 0
    details = usage.get("input_token_details") or {}
    read = details.get("cache_read") or 0 if cached else 0
    write = details.get("cache_creation") or 0 if cached else 0
    p = _tier(pricing, inp)
    prompt = float(p["prompt"])
    read_price = float(p.get("input_cache_read") or prompt)
    write_price = float(p.get("input_cache_write") or prompt)
    total = (inp - read - write) * prompt + read * read_price + write * write_price + out * float(p["completion"])
    # An endpoint's price can carry a discount (e.g. 0.51 on deepinfra/fp8); billing applies it to the whole call.
    return total * (1 - float(p.get("discount") or 0))
