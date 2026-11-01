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
        tasks.render("f3", 2)
    with pytest.raises(ValueError):
        tasks.render("f1", 0)


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
