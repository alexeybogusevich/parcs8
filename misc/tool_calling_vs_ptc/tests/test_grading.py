import pytest

from tool_calling_vs_ptc.agent import RunLog
from tool_calling_vs_ptc.grading import grade
from tool_calling_vs_ptc.runner import blame

F1_TRUTH = 0.25165212943066384
F2_TRUTH = 0.375


def layer(layer_id, parallelism=1, prev=(), status="Completed"):
    return {"layerId": layer_id, "parallelism": parallelism, "previousLayerIds": list(prev), "status": status}


def returned(layer_id, output):
    return {"name": "run_layer", "envelope": {"layerId": layer_id, "status": "Completed", "output": output}}


def f1_plan(n, total=F1_TRUTH):
    subs = [layer(f"s{k}") for k in range(n)]
    reduce = layer("r", prev=[s["layerId"] for s in subs])
    return [*subs, reduce], [returned("r", {"total": total, "parts": n})]


def f2_plan(n, workers=4, mid=F2_TRUTH):
    steps = [layer("t1", workers)] + [layer(f"t{j}", workers, [f"t{j - 1}"]) for j in range(2, n + 1)]
    final = layer("fin", 1, [f"t{n}"])
    return [*steps, final], [returned("fin", {"mid": mid, "lo": 0.25, "hi": 0.5})]


def test_f1_correct_plan_succeeds():
    layers, calls = f1_plan(4)

    g = grade("f1", 4, "ANSWER: 0.2516521294306638", layers, calls, F1_TRUTH, 4)

    assert g["success"] and g["correct"] and g["protocol_ok"], g["protocol_reasons"]
    assert g["layers_completed"] == 5 and g["layers_extra"] == 0


def test_f1_one_layer_too_few_is_a_protocol_failure_even_when_correct():
    layers, calls = f1_plan(3)
    layers[-1]["previousLayerIds"] = ["s0", "s1", "s2"]

    g = grade("f1", 4, "ANSWER: 0.2516521294306638", layers, calls, F1_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"] and not g["success"]


def test_f1_packed_into_one_wide_layer_fails():
    layers = [layer("map", 4), layer("r", prev=["map"])]
    calls = [returned("r", {"total": F1_TRUTH})]

    g = grade("f1", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert not g["protocol_ok"]


def test_f1_reduce_must_read_every_subinterval_layer():
    layers, calls = f1_plan(4)
    layers[-1]["previousLayerIds"] = ["s0", "s1", "s2"]

    g = grade("f1", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert not g["protocol_ok"]


def test_failed_attempts_do_not_break_the_plan():
    layers, calls = f1_plan(2)
    layers.insert(0, layer("crashed", status="Failed"))

    g = grade("f1", 2, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["success"] and g["layers_other"] == 1


def test_extra_layers_outside_the_answer_plan_are_counted_not_failed():
    layers, calls = f1_plan(2)
    layers.insert(0, layer("smoke-test"))

    g = grade("f1", 2, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["success"] and g["layers_extra"] == 1


def test_whole_plan_rerun_after_a_bug_fix_succeeds():
    buggy = [layer("a0"), layer("a1"), layer("ar", prev=["a0", "a1"])]
    fixed, calls = f1_plan(2)
    calls = [returned("ar", {"total": 0.0103}), *calls]

    g = grade("f1", 2, f"ANSWER: {F1_TRUTH}", buggy + fixed, calls, F1_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]
    assert g["layers_completed"] == 6 and g["layers_extra"] == 3


def test_wrong_answer_from_a_correct_plan_keeps_protocol_ok():
    layers, calls = f1_plan(2, total=0.0103)

    g = grade("f1", 2, "ANSWER: 0.0103", layers, calls, F1_TRUTH, 4)

    assert g["protocol_ok"] and not g["correct"] and not g["success"]


def test_answer_must_be_the_final_layer_output():
    layers, calls = f1_plan(2, total=0.3)

    g = grade("f1", 2, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"]
    assert "the reported answer is not the output of a plan's final layer" in g["protocol_reasons"]


def test_missing_answer_is_not_correct():
    layers, calls = f1_plan(2)

    g = grade("f1", 2, "I computed it.", layers, calls, F1_TRUTH, 4)

    assert g["answer"] is None and not g["correct"] and not g["success"]


def test_f2_correct_chain_succeeds():
    layers, calls = f2_plan(2)

    g = grade("f2", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]


def test_f2_answer_off_by_one_step_is_wrong():
    layers, calls = f2_plan(2, mid=0.3125)

    g = grade("f2", 2, "ANSWER: 0.3125", layers, calls, F2_TRUTH, 4)

    assert not g["correct"]


def test_f2_step_layers_must_use_max_parallelism():
    layers, calls = f2_plan(2, workers=1)

    g = grade("f2", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert not g["protocol_ok"]


def test_f2_steps_folded_into_fewer_layers_fail():
    layers, calls = f2_plan(1)

    g = grade("f2", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert not g["protocol_ok"]


def test_f2_side_branch_is_extra():
    layers, calls = f2_plan(2)
    layers.append(layer("side", 4, ["t1"]))

    g = grade("f2", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert g["success"] and g["layers_extra"] == 1


def test_f2_final_layer_reading_two_steps_fails():
    layers, calls = f2_plan(2)
    layers[-1]["previousLayerIds"] = ["t1", "t2"]

    g = grade("f2", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert not g["protocol_ok"]


def test_no_layers_at_all_fails():
    g = grade("f2", 2, "ANSWER: 0.375", [], [], F2_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"]


def test_broken_plan_with_a_lost_tool_call_is_blamed_on_infrastructure():
    lost = RunLog(tool_calls=[{"name": "run_layer", "exception": "ReadTimeout: ..."}])
    clean = RunLog()
    broken = {"protocol_ok": False}

    assert blame("answer", broken, lost, 5) == "infra_error"
    assert blame("answer", broken, clean, 5) == "answer"
    assert blame("answer", {"protocol_ok": True}, lost, 5) == "answer"
    assert blame("wall_budget", broken, lost, 5) == "wall_budget"


def test_run_without_a_model_reply_is_blamed_on_infrastructure():
    assert blame("error", {"protocol_ok": False}, RunLog(), 0) == "infra_error"


def f3_plan(n, total=F1_TRUTH):
    level = [layer(f"leaf{k}") for k in range(n)]
    layers, depth = list(level), 0
    while len(level) > 1:
        depth += 1
        level = [
            layer(f"c{depth}-{i}", prev=[level[2 * i]["layerId"], level[2 * i + 1]["layerId"]])
            for i in range(len(level) // 2)
        ]
        layers += level
    return layers, [returned(level[0]["layerId"], {"sum": total})]


@pytest.mark.parametrize("n", [2, 4, 8, 32])
def test_f3_balanced_tree_succeeds(n):
    layers, calls = f3_plan(n)

    g = grade("f3", n, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]
    assert g["layers_completed"] == 2 * n - 1 and g["layers_extra"] == 0


def test_f3_flat_reduce_fails():
    layers, calls = f1_plan(4)

    g = grade("f3", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"]


def test_f3_unbalanced_tree_fails():
    # ((l0 + l1) + l2) + l3: three combine layers, but the leaves sit at depths 3, 3, 2 and 1.
    layers = [layer(f"l{k}") for k in range(4)]
    layers += [layer("a", prev=["l0", "l1"]), layer("b", prev=["a", "l2"]), layer("root", prev=["b", "l3"])]
    calls = [returned("root", {"sum": F1_TRUTH})]

    g = grade("f3", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert not g["protocol_ok"]


def test_f3_leaf_shared_by_two_combines_fails():
    layers, calls = f3_plan(4)
    layers[5]["previousLayerIds"] = ["leaf0", "leaf1"]

    g = grade("f3", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert not g["protocol_ok"]


def spawn_layer(n, spawned=None, parallelism=1):
    row = layer("tree", parallelism)
    row.update({"maxPoints": 2 * n - 1, "spawnedPoints": 2 * n - 2 if spawned is None else spawned})
    return [row], [returned("tree", {"sum": F1_TRUTH})]


def test_f3s_one_layer_spawning_the_whole_tree_succeeds():
    layers, calls = spawn_layer(8)

    g = grade("f3s", 8, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]


@pytest.mark.parametrize("spawned", [0, 2, 13])
def test_f3s_spawning_a_different_tree_fails(spawned):
    layers, calls = spawn_layer(8, spawned)
    if not spawned:
        del layers[0]["spawnedPoints"]

    g = grade("f3s", 8, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"]


def test_f3s_as_a_tree_of_layers_fails():
    layers, calls = f3_plan(4)

    g = grade("f3s", 4, f"ANSWER: {F1_TRUTH}", layers, calls, F1_TRUTH, 4)

    assert not g["protocol_ok"]


def peer_layer(workers=4, channels=3, mid=F2_TRUTH):
    row = layer("bisect", workers)
    if channels:
        row["peerChannels"] = channels
    outputs = [{"mid": mid}] + [{"worker": w} for w in range(1, workers)]
    call = {"name": "get_layer_result", "envelope": {"layerId": "bisect", "status": "Completed", "outputs": outputs}}
    return [row], [call]


@pytest.mark.parametrize("channels", [3, 4, 6])
def test_f2p_one_layer_with_a_connected_reduction_succeeds(channels):
    layers, calls = peer_layer(channels=channels)

    g = grade("f2p", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]


def test_f2p_reads_answers_from_unparsed_worker_outputs():
    layers, calls = peer_layer()
    calls[0]["envelope"]["outputs"] = ['{"mid": 0.375}', None, None, None]

    g = grade("f2p", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert g["success"], g["protocol_reasons"]


@pytest.mark.parametrize(("workers", "channels"), [(4, 0), (4, 2), (1, 0)])
def test_f2p_without_a_reduction_over_every_worker_fails(workers, channels):
    layers, calls = peer_layer(workers, channels)

    g = grade("f2p", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert g["correct"] and not g["protocol_ok"]


def test_f2p_as_a_layer_per_step_fails():
    layers, calls = f2_plan(2)

    g = grade("f2p", 2, "ANSWER: 0.375", layers, calls, F2_TRUTH, 4)

    assert not g["protocol_ok"]
