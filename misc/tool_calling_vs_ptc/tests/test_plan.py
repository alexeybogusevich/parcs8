from tool_calling_vs_ptc.plan import Cell, append_record, load_records, ordered_cells, pending

ARGS = (["f1", "f2"], [2, 4, 8, 16, 32], ["tool_calling", "ptc"], ["a", "b", "c"], 5)


def test_every_cell_once():
    cells = ordered_cells(*ARGS, seed=1)

    assert len(cells) == 2 * 5 * 2 * 3 * 5
    assert len({c.run_id for c in cells}) == len(cells)


def test_order_is_seeded_and_interleaves_arms():
    cells = ordered_cells(*ARGS, seed=1)

    assert cells == ordered_cells(*ARGS, seed=1)
    assert cells != ordered_cells(*ARGS, seed=2)
    for a, b in zip(cells[::2], cells[1::2], strict=True):
        assert (a.family, a.n, a.model, a.rep) == (b.family, b.n, b.model, b.rep)
        assert {a.arm, b.arm} == {"tool_calling", "ptc"}
    firsts = [c.arm for c in cells[::2]]
    assert 0.3 < firsts.count("ptc") / len(firsts) < 0.7


def test_resume_skips_recorded_runs_but_repeats_infra_errors(tmp_path):
    cells = ordered_cells(["f1"], [2], ["tool_calling", "ptc"], ["a"], 2, seed=1)
    runs = tmp_path / "runs.jsonl"
    done, flaky, failed = cells[0], cells[1], cells[2]
    append_record(runs, {"run_id": done.run_id, "termination": "answer"})
    append_record(runs, {"run_id": flaky.run_id, "termination": "infra_error"})
    append_record(runs, {"run_id": failed.run_id, "termination": "recursion_limit"})

    assert pending(cells, load_records(runs)) == [flaky, cells[3]]

    append_record(runs, {"run_id": flaky.run_id, "termination": "answer"})
    assert pending(cells, load_records(runs)) == [cells[3]]


def test_run_id_names_the_cell():
    assert Cell("f2", 16, "ptc", "small", 3).run_id == "f2-n16-ptc-small-r3"
