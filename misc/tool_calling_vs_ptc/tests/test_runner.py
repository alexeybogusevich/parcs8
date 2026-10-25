"""Both arms end to end against a fake PARCS MCP server: a scripted model, the real middleware stack,
the interceptor log, the server's layer list and the grade."""

import json

import httpx
import pytest
from langchain_mcp_adapters.interceptors import MCPToolCallRequest
from openai import InternalServerError, NotFoundError, RateLimitError

from tool_calling_vs_ptc import agent as agent_mod
from tool_calling_vs_ptc.cluster import Cluster, pool
from tool_calling_vs_ptc.config import Config, ModelSpec
from tool_calling_vs_ptc.plan import Cell
from tool_calling_vs_ptc.runner import SweepAborted, classify, execute

from .conftest import CLUSTER_INFO, ScriptModel, ai, last_envelope, scripted

TRUTH = 0.25165212943066384  # F1; each fake subinterval returns half of it
HALF = TRUTH / 2
SOURCE = "class Sub {} class Sum {}"  # the same in every run, as at temperature 0


def tool_calling_model() -> ScriptModel:
    """Sizes the cluster, compiles, fans out two subintervals in one turn, reduces, answers."""

    def step(messages):
        turn = sum(1 for m in messages if m.type == "ai")
        if turn == 0:
            return ai([{"name": "get_cluster_info", "args": {}}], turn)
        if turn == 1:
            return ai([{"name": "create_session", "args": {"sourceCode": SOURCE}}], turn)
        sid = last_envelope(messages, "create_session")["sessionId"]
        if turn == 2:
            sub = {"sessionId": sid, "entryPoint": "Sub", "parallelism": 1, "parameters": {"v": repr(HALF)}}
            return ai([{"name": "run_layer", "args": {**sub, "layerKey": f"sub-{k}"}} for k in range(2)], turn)
        if turn == 3:
            prev = [f"{sid}/sub-0", f"{sid}/sub-1"]
            args = {
                "sessionId": sid,
                "entryPoint": "Sum",
                "parallelism": 1,
                "layerKey": "sum",
                "previousLayerIds": prev,
            }
            return ai([{"name": "run_layer", "args": args}], turn)
        return ai(f"ANSWER: {last_envelope(messages, 'run_layer')['output']['total']!r}")

    return ScriptModel(script=step, system_prompts=[])


def ptc_model() -> ScriptModel:
    code = f"""{{
const call = async (fn, args) => JSON.parse((await fn(args))[0].text);
const info = await call(tools.getClusterInfo, {{}});
const s = await call(tools.createSession, {{ sourceCode: "{SOURCE}" }});
const subs = await Promise.all([0, 1].map(k => call(tools.runLayer, {{ sessionId: s.sessionId, entryPoint: "Sub",
  parallelism: 1, layerKey: "sub-" + k, parameters: {{ v: "{HALF!r}" }} }})));
const red = await call(tools.runLayer, {{ sessionId: s.sessionId, entryPoint: "Sum", parallelism: 1,
  layerKey: "sum", previousLayerIds: subs.map(x => x.layerId) }});
red.output.total;
}}"""
    return scripted([{"name": "eval", "args": {"code": code}}], f"ANSWER: {TRUTH!r}")


MODELS = {"tool_calling": tool_calling_model, "ptc": ptc_model}


@pytest.fixture
def config(fake_parcs, tmp_path):
    url, _ = fake_parcs
    return Config(mcp={"cluster_url": url}, experiment={"results_dir": tmp_path})


async def run(config, monkeypatch, arm, model=None, rep=1, n=2):
    model = model or MODELS[arm]()
    monkeypatch.setattr(agent_mod, "create_model", lambda llm: model)
    cell = Cell("f1", n, arm, "fake", rep)
    manifest = {"pool": pool(CLUSTER_INFO)}
    cluster = Cluster(config.mcp.cluster_url)
    spec = ModelSpec(id="fake", model_name="fake")
    record = await execute(cell, config, spec, cluster, manifest, "test", config.experiment.results_dir)
    return record, model


def transcript(config, record):
    return json.loads((config.experiment.results_dir / record["transcript"]).read_text(encoding="utf-8"))


@pytest.mark.parametrize("arm", ["tool_calling", "ptc"])
async def test_run_is_logged_and_graded(config, monkeypatch, arm):
    record, _ = await run(config, monkeypatch, arm)

    assert record["termination"] == "answer", record["error"]
    assert record["success"] and record["protocol_ok"] and record["layers_completed"] == 3
    assert record["llm_calls"] == (5 if arm == "tool_calling" else 2)

    t = transcript(config, record)
    names = [c["name"] for c in t["tool_calls"]]
    # In ptc the PARCS calls happen inside `eval`; the interceptor sees them all the same.
    assert sorted(names) == ["create_session", "get_cluster_info", "run_layer", "run_layer", "run_layer"]
    assert all(c["envelope"]["ok"] for c in t["tool_calls"])
    assert len(t["model_calls"]) == record["llm_calls"]
    assert t["messages"][0]["data"]["content"].startswith("Compute I")


@pytest.mark.parametrize("arm", ["tool_calling", "ptc"])
async def test_repetitions_with_identical_source_do_not_share_sessions_or_layers(config, monkeypatch, arm):
    first, _ = await run(config, monkeypatch, arm, rep=1)
    second, _ = await run(config, monkeypatch, arm, rep=2)

    sessions = [{c["envelope"]["sessionId"] for c in transcript(config, r)["tool_calls"] if c["name"] == "run_layer"}
                for r in (first, second)]  # fmt: skip
    assert len(sessions[0]) == len(sessions[1]) == 1 and sessions[0] != sessions[1]
    for record in (first, second):
        assert record["success"] and record["layers_completed"] == 3
        assert not any(c["envelope"].get("reused") for c in transcript(config, record)["tool_calls"])


async def test_each_run_gets_its_own_thread_and_nonce(config, monkeypatch):
    first, model_a = await run(config, monkeypatch, "tool_calling", rep=1)
    second, model_b = await run(config, monkeypatch, "tool_calling", rep=2)

    assert first["thread_id"] != second["thread_id"]
    nonce_a, nonce_b = (m.system_prompts[0].splitlines()[0] for m in (model_a, model_b))
    assert nonce_a.startswith("Run nonce:") and nonce_a != nonce_b
    # Within one run the nonce is stable, so the provider can cache the prefix across turns.
    assert {p.splitlines()[0] for p in model_a.system_prompts} == {nonce_a}


async def test_pool_change_aborts_the_sweep(config, monkeypatch):
    monkeypatch.setattr(agent_mod, "create_model", lambda llm: scripted("ANSWER: 0"))
    manifest = {"pool": {**pool(CLUSTER_INFO), "currentCapacity": 8}}

    with pytest.raises(SystemExit, match="pool changed"):
        await execute(Cell("f1", 2, "ptc", "fake", 1), config, ModelSpec(id="fake", model_name="fake"),
                      Cluster(config.mcp.cluster_url), manifest, "test", config.experiment.results_dir)  # fmt: skip


class AuthenticationError(Exception):
    """Named like the provider SDKs' error for a bad key."""


class APIConnectionError(Exception):
    """Named like the provider SDKs' error for an unreachable endpoint."""


def failing_model(ex: Exception) -> ScriptModel:
    def step(messages):
        raise ex

    return ScriptModel(script=step, system_prompts=[])


async def test_bad_key_aborts_the_sweep(config, monkeypatch):
    with pytest.raises(SweepAborted, match="AuthenticationError"):
        await run(config, monkeypatch, "ptc", model=failing_model(AuthenticationError("bad key")))


async def test_unreachable_model_is_an_infra_error(config, monkeypatch):
    record, _ = await run(config, monkeypatch, "ptc", model=failing_model(APIConnectionError("down")))

    assert record["termination"] == "infra_error" and record["llm_calls"] == 0


def scavenger_model(foreign_layer: str) -> ScriptModel:
    """Skips the work: lists the server's sessions and reads a finished layer it did not run."""

    def step(messages):
        turn = sum(1 for m in messages if m.type == "ai")
        if turn == 0:
            return ai([{"name": "list_sessions", "args": {}}], turn)
        if turn == 1:
            return ai([{"name": "get_layer_result", "args": {"layerId": foreign_layer}}], turn)
        return ai(f"ANSWER: {TRUTH!r}")

    return ScriptModel(script=step, system_prompts=[])


async def test_a_run_cannot_see_or_read_another_runs_sessions(config, monkeypatch, fake_parcs):
    _, parcs = fake_parcs
    first, _ = await run(config, monkeypatch, "tool_calling", rep=1)
    foreign = next(i for i, layer in parcs.layers.items() if layer["row"]["entryPoint"] == "Sum")

    second, _ = await run(config, monkeypatch, "tool_calling", model=scavenger_model(foreign), rep=2)

    assert first["success"] and not second["success"] and second["layers_completed"] == 0
    listed, read = transcript(config, second)["tool_calls"]
    assert listed["envelope"]["output"]["sessions"] == []
    assert read["foreign"] and read["envelope"]["error"]["code"] == "layer_not_found"


def failing_layer_model() -> ScriptModel:
    """Runs a layer that fails, then reads its diagnostics, as a model fixing a worker error does."""

    def step(messages):
        turn = sum(1 for m in messages if m.type == "ai")
        if turn == 0:
            return ai([{"name": "create_session", "args": {"sourceCode": SOURCE}}], turn)
        sid = last_envelope(messages, "create_session")["sessionId"]
        if turn == 1:
            args = {"sessionId": sid, "entryPoint": "Fail", "parallelism": 1, "layerKey": "boom"}
            return ai([{"name": "run_layer", "args": args}], turn)
        if turn == 2:
            return ai([{"name": "get_layer_result", "args": {"layerId": f"{sid}/boom", "view": "full"}}], turn)
        return ai("ANSWER: 0")

    return ScriptModel(script=step, system_prompts=[])


async def test_a_runs_own_failed_layer_stays_readable(config, monkeypatch):
    record, _ = await run(config, monkeypatch, "tool_calling", model=failing_layer_model())

    _, started, read = transcript(config, record)["tool_calls"]
    assert started["envelope"]["status"] == "Failed"
    assert not read.get("foreign") and read["envelope"]["error"]["code"] == "worker_error"


async def test_a_parent_whose_start_response_was_lost_is_found_on_the_server(fake_parcs):
    url, parcs = fake_parcs
    sid = "s-lost-response"
    parcs.run_layer(sid, "Sub", 1, "lost", {"v": "1.0"})  # started, but the run never saw the answer
    log = agent_mod.RunLog(cluster_url=url, own_sessions={sid: None})
    calls = []

    async def handler(request):
        calls.append(request)
        return agent_mod._result({"ok": True, "sessionId": sid, "layerId": f"{sid}/sum", "status": "Completed"})

    args = {"sessionId": sid, "entryPoint": "Sum", "parallelism": 1, "previousLayerIds": [f"{sid}/lost"]}
    result, refused = await agent_mod.private_view(MCPToolCallRequest("run_layer", args, "parcs"), handler, log)

    assert not refused and len(calls) == 1 and f"{sid}/lost" in log.own_layers
    foreign = {**args, "previousLayerIds": ["s-other/x"]}
    _, refused = await agent_mod.private_view(MCPToolCallRequest("run_layer", foreign, "parcs"), handler, log)
    assert refused and len(calls) == 1


@pytest.mark.parametrize("output", [0.25, [1, 2], "text", None, True])
def test_layer_outputs_that_are_not_objects_are_tracked(output):
    # A computation may return a bare double; the interceptor once raised on it after the layer ran.
    log = agent_mod.RunLog()
    log.remember("create_session", {"ok": True, "sessionId": "s"})
    log.remember("run_layer", {"ok": True, "sessionId": "s", "layerId": "l", "status": "Completed", "output": output})
    assert log.own_layers == {"l"}


def _status_error(cls, status: int):
    request = httpx.Request("POST", "https://openrouter.ai/api/v1/chat/completions")
    return cls("provider says no", response=httpx.Response(status, request=request), body=None)


def test_wrapped_provider_errors_are_classified_by_class_hierarchy():
    # langchain-openai re-raises openai errors as subclasses (OpenAIRateLimitError, ...); a 429 in the
    # middle of a run once counted against the model.
    class OpenAIRateLimitError(RateLimitError):
        pass

    class OpenAINotFoundError(NotFoundError):
        pass

    assert classify(_status_error(OpenAIRateLimitError, 429)) == "infra_error"
    assert classify(_status_error(InternalServerError, 503)) == "infra_error"
    assert classify(ValueError("model wrote bad code")) == "error"
    with pytest.raises(SweepAborted):
        classify(_status_error(OpenAINotFoundError, 404))


async def test_a_budget_spent_waiting_on_a_hung_call_is_an_infra_error(config, monkeypatch):
    monkeypatch.setattr(agent_mod, "HUNG_FLOOR_SECONDS", 0.0)
    monkeypatch.setattr(agent_mod, "HUNG_GRACE_SECONDS", 0.5)
    config.experiment.wall_budget_seconds = 1.5

    def step(messages):
        turn = sum(1 for m in messages if m.type == "ai")
        if turn == 0:
            return ai([{"name": "create_session", "args": {"sourceCode": SOURCE}}], turn)
        sid = last_envelope(messages, "create_session")["sessionId"]
        args = {"sessionId": sid, "entryPoint": "Hang", "parallelism": 1, "layerKey": f"hang{turn}"}
        return ai([{"name": "run_layer", "args": args}], turn)

    record, _ = await run(config, monkeypatch, "tool_calling", model=ScriptModel(script=step, system_prompts=[]))

    assert record["termination"] == "infra_error"
    assert "hung tool call: run_layer" in record["error"]


def test_a_call_within_its_own_wait_is_not_hung():
    log = agent_mod.RunLog()
    log.in_flight[1] = {"name": "run_layer", "args": {"waitSeconds": 200}, "t_start": 0.0}
    log.in_flight[2] = {"name": "create_session", "args": {}, "t_start": 0.0}
    assert [c["name"] for c in log.hung(now=150.0)] == ["create_session"]
    assert [c["name"] for c in log.hung(now=300.0)] == ["run_layer", "create_session"]


async def test_streamed_chunks_are_counted_on_the_call_in_progress():
    from langchain_core.messages import AIMessageChunk
    from langchain_core.outputs import ChatGenerationChunk

    log = agent_mod.RunLog()
    agent_mod.RECORDER.current = log
    try:
        log.model_calls.append({"t_start": 0.0, "chunks": 0, "chars": 0, "t_first_chunk": None})
        counter = agent_mod.StreamCounter()
        await counter.on_llm_new_token("abc")
        tool_chunk = AIMessageChunk(content="", tool_call_chunks=[{"name": "eval", "args": '{"code"', "index": 0}])
        await counter.on_llm_new_token("", chunk=ChatGenerationChunk(message=tool_chunk))
    finally:
        agent_mod.RECORDER.current = None
    assert log.model_calls[0]["chunks"] == 2 and log.model_calls[0]["chars"] == 3 + len('{"code"')
    assert log.model_calls[0]["t_first_chunk"] is not None
