import pytest

from tool_calling_vs_ptc import tasks


@pytest.mark.parametrize("family", ["f1", "f2"])
@pytest.mark.parametrize("n", tasks.N_GRID)
def test_render_fixes_n_and_layer_count(family, n):
    task = tasks.render(family, n)

    assert task.expected_layers == n + 1
    assert f"N = {n}" in task.text and f"exactly {n + 1} layers" in task.text
    assert task.text.rstrip().endswith("ANSWER: <number>")


@pytest.mark.parametrize("family", ["f1", "f2"])
def test_render_differs_only_where_n_appears(family):
    small, large = tasks.render(family, 2).text.splitlines(), tasks.render(family, 32).text.splitlines()

    assert len(small) == len(large)
    assert all(any(x in b for x in ("31", "32", "33")) for a, b in zip(small, large, strict=True) if a != b)


def test_render_rejects_unknown_family_and_bad_n():
    with pytest.raises(ValueError):
        tasks.render("f9", 2)
    with pytest.raises(ValueError):
        tasks.render("f1", 0)
    with pytest.raises(ValueError):
        tasks.render("f3", 6)
    with pytest.raises(ValueError):
        tasks.render("f3s", 16)


@pytest.mark.parametrize(
    ("family", "layers"),
    [("f3", lambda n: 2 * n - 1), ("f3s", lambda n: 1), ("f2p", lambda n: 1)],
)
@pytest.mark.parametrize("n", tasks.N_GRID)
def test_render_new_families_fix_n_and_the_plan(family, layers, n):
    if not tasks.supports(family, n):
        return
    task = tasks.render(family, n)

    assert task.expected_layers == layers(n)
    assert f"N = {n}" in task.text and f"exactly {layers(n)} layer" in task.text
    assert task.text.rstrip().endswith("ANSWER: <number>")
    assert "{" not in task.text.replace("new[] { leftInput", "")


def test_f3s_budget_fits_the_spawn_tree():
    for n in (2, 4, 8):
        text = tasks.render("f3s", n).text
        assert f"maxPoints = {2 * n - 1}" in text and f"maxDepth = {tasks.levels(n)}" in text
        assert f"spawns {2 * n - 2} points" in text


def test_tree_families_share_the_integral_and_f2p_the_bisection():
    assert tasks.problem("f3") == tasks.problem("f3s") == "f1"
    assert tasks.problem("f2p") == "f2"
    assert tasks.tolerance("f3s", 8) == tasks.tolerance("f1", 8)
    assert tasks.tolerance("f2p", 16) == tasks.tolerance("f2", 16)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("I = 0.25\n\nANSWER: 0.2516521294306638", 0.2516521294306638),
        ("ANSWER: 1e-3", 1e-3),
        ("ANSWER: -.5", -0.5),
        ("answer: 0.375", 0.375),
        ("**ANSWER:** 0.375", 0.375),
        ("**ANSWER: 0.375**", 0.375),
        ("ANSWER: `0.375`", 0.375),
        ("ANSWER: I = 0.2516", 0.2516),
        ("ANSWER: 1\nmore text\nANSWER: 2", 2.0),
    ],
)
def test_parse_answer_accepts_common_formats(text, expected):
    assert tasks.parse_answer(text) == expected


@pytest.mark.parametrize("text", ["", "The answer is 0.25", "ANSWER: about a quarter", "ANSWER: inf", None])
def test_parse_answer_rejects_missing_or_non_numeric(text):
    assert tasks.parse_answer(text) is None


def test_f2_tolerance_separates_neighbouring_step_counts():
    for n in tasks.N_GRID:
        # The midpoints after n and n + 1 steps are 2^-(n+2) apart.
        assert tasks.f2_tolerance(n) * 2 < 2.0 ** -(n + 2)
