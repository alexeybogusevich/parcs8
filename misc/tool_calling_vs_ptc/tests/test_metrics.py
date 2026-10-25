"""Metrics extraction: synthetic cases, both arms end to end, and the exploratory run's published table."""

import json

import pytest

from tool_calling_vs_ptc import agent as agent_mod
from tool_calling_vs_ptc.analysis.cli import EXPLORATORY, FIXTURES, crosscheck, exploratory_rows
from tool_calling_vs_ptc.analysis.metrics import _union, run_metrics, turns
from tool_calling_vs_ptc.analysis.normalize import ModelCall, ParcsCall, Run, ToolResult, from_transcript
from tool_calling_vs_ptc.analysis.pricing import call_cost
from tool_calling_vs_ptc.cluster import Cluster, pool
from tool_calling_vs_ptc.config import Config, ModelSpec
from tool_calling_vs_ptc.plan import Cell
from tool_calling_vs_ptc.runner import execute

from .conftest import CLUSTER_INFO
from .test_runner import ptc_model, tool_calling_model

LUNA = {"prompt": "0.0000002", "completion": "0.0000012", "input_cache_read": "0.00000002",
        "input_cache_write": "0.00000025",
        "overrides": [{"min_prompt_tokens": 272000, "prompt": "0.0000004", "completion": "0.0000018"}]}  # fmt: skip


def usage(inp, out, read=0, write=0):
    return {
        "input_tokens": inp,
        "output_tokens": out,
        "input_token_details": {"cache_read": read, "cache_creation": write},
    }


def test_cost_prices_each_cache_tier():
    # LangSmith's price map, for the same call: 0.00037245 USD.
    assert call_cost(usage(7677, 117, read=7335, write=339), LUNA) == pytest.approx(0.00037245, abs=1e-8)
    assert call_cost(usage(1000, 0, read=1000), LUNA, cached=False) == pytest.approx(1000 * 2e-7)


def test_cost_uses_the_long_context_tier_and_unknown_models_have_none():
    assert call_cost(usage(300_000, 10), LUNA) == pytest.approx(300_000 * 4e-7 + 10 * 1.8e-6)
    assert call_cost(usage(10, 10), None) is None


def test_union_counts_overlapping_parallel_calls_once():
    assert _union([(0, 2), (1, 3), (5, 6)]) == 4


def test_parcs_calls_are_assigned_to_the_turn_that_made_them():
    calls = [ModelCall({}, [], 0, 1), ModelCall({}, [], 3, 4), ModelCall({}, [], 6, 7)]
    parcs = [ParcsCall("run_layer", {}, {}, 1.5, 2), ParcsCall("run_layer", {}, {}, 4.5, 5)]
    run = Run("r", "tool_calling", "m", calls=calls, parcs=parcs)

    assert turns(run) == [0, 1]


def test_eval_errors_are_counted_by_type():
    run = Run("r", "ptc", "m", tool_results=[
        ToolResult("eval", "success", '<error type="SyntaxError">return outside function</error>'),
        ToolResult("eval", "success", "<result>4</result>"),
        ToolResult("eval", "error", '<error type="Error">at call</error>'),
        ToolResult("eval", "success", '<stdout>\nstep 1 done\n</stdout><error type="TypeError">x is undefined</error>'),
        ToolResult("eval", "success", '<stdout>\n<error type="Fake">\n</stdout><result>1</result>')])  # fmt: skip

    m = run_metrics(run, {"models": {}})

    assert m["evals"] == 5 and m["eval_errors"] == 3 and m["eval_syntax_errors"] == 1
    assert json.loads(m["eval_error_types"]) == {"SyntaxError": 1, "Error": 1, "TypeError": 1}


@pytest.fixture
def config(fake_parcs, tmp_path):
    url, _ = fake_parcs
    return Config(mcp={"cluster_url": url}, experiment={"results_dir": tmp_path})


async def metrics_of(config, monkeypatch, arm, model):
    monkeypatch.setattr(agent_mod, "create_model", lambda llm: model)
    cell = Cell("f1", 2, arm, "fake", 1)
    record = await execute(cell, config, ModelSpec(id="fake", model_name="fake"), Cluster(config.mcp.cluster_url),
                           {"pool": pool(CLUSTER_INFO)}, "test", config.experiment.results_dir)  # fmt: skip
    t = json.loads((config.experiment.results_dir / record["transcript"]).read_text(encoding="utf-8"))
    return run_metrics(from_transcript(t, record), {"models": {"fake": LUNA}}), record


async def test_tool_calling_structure(config, monkeypatch):
    m, record = await metrics_of(config, monkeypatch, "tool_calling", tool_calling_model())

    assert m["success"] == record["success"] is True
    assert m["llm_calls"] == 5 and m["evals"] == 0 and m["parcs_calls"] == 5
    # Both subintervals in one message, then the reduce in the next: the plan took 2 turns.
    assert m["max_layer_batch"] == 2 and m["fanout_batched"] and m["plan_turns"] == 2
    assert m["layers_started"] == m["layers_completed"] == 3 and m["foreign_calls"] == 0
    assert m["wall_s"] == record["wall_s"] and m["model_s"] > 0 and m["parcs_s"] > 0


async def test_ptc_structure(config, monkeypatch):
    m, _ = await metrics_of(config, monkeypatch, "ptc", ptc_model())

    assert m["success"] and m["llm_calls"] == 2 and m["evals"] == 1 and m["eval_errors"] == 0
    # The whole plan came from the one eval.
    assert m["plan_turns"] == 1 and m["plan_in_one_turn"] and m["max_layer_batch"] == 0


@pytest.mark.parametrize("label", list(EXPLORATORY))
def test_extractor_reproduces_the_exploratory_table(label):
    calls, prompt, completion, cost, wall, layers, evals = EXPLORATORY[label][1]
    m = exploratory_rows()[label]

    assert (m["llm_calls"], m["input_tokens"], m["output_tokens"]) == (calls, prompt, completion)
    assert round(m["cost_usd"], 4) == cost and m["cost_usd"] == pytest.approx(m["langsmith_cost_usd"], rel=1e-6)
    assert abs(m["wall_s"] - wall) <= 1
    # The published table says 14 layers for both pi runs; their traces hold 13 run_layer calls each
    # (12 sample sizes and a reduce), so the table is off by one there.
    assert m["layers_started"] == (13 if label.startswith("pi-") else layers)
    assert m["evals"] == (evals or 0)


def test_fixtures_hold_no_prompts_or_sources():
    for path in FIXTURES.glob("*-*.json"):
        text = path.read_text(encoding="utf-8")
        assert "IAgentComputation" not in text and "## PARCS cluster" not in text, path.name


CROSSCHECK_FIXTURES = FIXTURES.parent / "crosscheck"


@pytest.mark.parametrize("run_id", ["f1-n2-ptc-luna-r1", "f2-n2-tool_calling-luna-r2"])
def test_transcript_and_trace_agree(run_id):
    transcript = json.loads((CROSSCHECK_FIXTURES / f"{run_id}.transcript.json").read_text(encoding="utf-8"))
    trace = json.loads((CROSSCHECK_FIXTURES / f"{run_id}.trace.json").read_text(encoding="utf-8"))
    prices = json.loads((CROSSCHECK_FIXTURES / "pricing.json").read_text(encoding="utf-8"))

    assert crosscheck(transcript, trace, prices) == {}


def test_regrading_catches_a_run_that_read_another_runs_layer():
    # Smoke run before the private view: every eval failed, then it read a finished layer of an
    # earlier run's session through list_sessions + get_layer_result. The runner graded it a success.
    transcript = json.loads((CROSSCHECK_FIXTURES / "f1-n2-ptc-luna-r1.transcript.json").read_text(encoding="utf-8"))
    prices = json.loads((CROSSCHECK_FIXTURES / "pricing.json").read_text(encoding="utf-8"))

    m = run_metrics(from_transcript(transcript), prices)

    assert transcript["grade"]["success"] and not m["success"]
    assert m["foreign_calls"] == 2 and m["list_sessions_calls"] == 2 and m["eval_errors"] == 3


def test_endpoint_discount_applies_to_the_whole_call():
    discounted = {**LUNA, "discount": 0.5}
    assert call_cost(usage(1000, 100), discounted) == pytest.approx(call_cost(usage(1000, 100), LUNA) / 2)
