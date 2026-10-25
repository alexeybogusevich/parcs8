"""Ground truth for both task families, computed offline and independently of the C# reference.

usage: uv run --with numpy --with mpmath python scripts/ground_truth.py [--out tasks/ground_truth.json]

F1: the integral at 30 digits (mpmath), plus the float64 composite-Simpson result a protocol-following
run gets for each N, which must sit well inside the tolerance.
F2: the exact bisection midpoints (dyadic, so exact in float64). Checks that no v_i equals a midpoint
(so `<=` versus `<` cannot matter) and reports from which step a uniform-distribution guess goes wrong.
"""

import argparse
import json
import math
import sys
from pathlib import Path

import mpmath
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))
from tool_calling_vs_ptc import tasks  # noqa: E402

MASK = (1 << 64) - 1


def f_mp(x):
    return mpmath.exp(-x / 4) * mpmath.cos(x + mpmath.mpf("0.5") * mpmath.sin(2 * x))


def f_np(x):
    return np.exp(-x / 4) * np.cos(x + 0.5 * np.sin(2 * x))


def simpson(a, b, panels):
    x = np.linspace(a, b, panels + 1)
    w = np.ones(panels + 1)
    w[1:-1:2] = 4
    w[2:-1:2] = 2
    return float((b - a) / panels / 3 * np.dot(w, f_np(x)))


def f1(grid):
    mpmath.mp.dps = 30
    exact = mpmath.quad(f_mp, mpmath.linspace(tasks.F1_A, tasks.F1_B, 33))
    rows = {}
    for n in grid:
        edges = [tasks.F1_A + (tasks.F1_B - tasks.F1_A) * k / n for k in range(n + 1)]
        parts = [simpson(edges[k], edges[k + 1], tasks.F1_PANELS) for k in range(n)]
        total = math.fsum(parts)
        err = abs(total - float(exact))
        assert err < tasks.F1_ABS_TOL / 100, (n, err)
        rows[n] = {
            "answer": float(exact),
            "tolerance": tasks.F1_ABS_TOL,
            "simpson_float64": total,
            "simpson_abs_error": err,
        }
    return {"exact_30_digits": mpmath.nstr(exact, 30), "per_n": rows}


def mix_py(i):
    z = (i * 0x9E3779B97F4A7C15) & MASK
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
    return z ^ (z >> 31)


def values():
    i = np.arange(1, tasks.F2_M + 1, dtype=np.uint64)
    with np.errstate(over="ignore"):
        z = i * np.uint64(0x9E3779B97F4A7C15)
        z = (z ^ (z >> np.uint64(30))) * np.uint64(0xBF58476D1CE4E5B9)
        z = (z ^ (z >> np.uint64(27))) * np.uint64(0x94D049BB133111EB)
        z = z ^ (z >> np.uint64(31))
    x = z % np.uint64(tasks.F2_P)
    for j in (1, 2, 3, 12345, tasks.F2_M):
        assert int(x[j - 1]) == mix_py(j) % tasks.F2_P, j
    return x.astype(np.float64) / float(tasks.F2_P)


def bisect(steps, count):
    lo, hi, trace = 0.0, 1.0, []
    for _ in range(steps):
        mid = (lo + hi) / 2
        c = count(mid)
        trace.append((mid, c))
        if c >= tasks.F2_K:
            hi = mid
        else:
            lo = mid
    return (lo + hi) / 2, trace


def f2(grid):
    v = np.sort(values())
    nmax = max(grid)
    exact_count = lambda mid: int(np.searchsorted(v, mid, side="right"))  # noqa: E731
    _, trace = bisect(nmax, exact_count)
    ties = [
        j + 1
        for j, (mid, _) in enumerate(trace)
        if np.searchsorted(v, mid, side="right") != np.searchsorted(v, mid, side="left")
    ]
    assert not ties, f"some v_i equals the midpoint of step(s) {ties}"
    # A model that assumes uniform v (count = M * mid) gets the same midpoints up to some step.
    uniform_count = lambda mid: tasks.F2_M * mid  # noqa: E731
    _, guess = bisect(nmax, uniform_count)
    first_wrong = next(
        (
            j + 1
            for j, (a, b) in enumerate(zip(trace, guess, strict=True))
            if (a[1] >= tasks.F2_K) != (b[1] >= tasks.F2_K)
        ),
        None,
    )
    kth = float(v[tasks.F2_K - 1])
    rows = {}
    for n in grid:
        answer, _ = bisect(n, exact_count)
        rows[n] = {
            "answer": answer,
            "answer_repr": repr(answer),
            "tolerance": tasks.f2_tolerance(n),
            "uniform_guess_correct": first_wrong is None or n < first_wrong,
        }
    return {
        "kth_smallest": kth,
        "uniform_guess_first_wrong_step": first_wrong,
        "min_abs_count_minus_k": min(abs(c - tasks.F2_K) for _, c in trace),
        "trace": [{"step": j + 1, "mid": m, "count": c} for j, (m, c) in enumerate(trace)],
        "per_n": rows,
    }


def main():
    here = Path(__file__).resolve().parents[1]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", type=Path, default=here / "tasks" / "ground_truth.json")
    args = ap.parse_args()
    grid = list(tasks.N_GRID)
    truth = {"f1": f1(grid), "f2": f2(grid)}
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(truth, indent=2) + "\n", encoding="utf-8")
    summary = {fam: {n: r["answer"] for n, r in t["per_n"].items()} for fam, t in truth.items()}
    print(json.dumps(summary, indent=2))
    print("f2 uniform guess first wrong at step", truth["f2"]["uniform_guess_first_wrong_step"])


if __name__ == "__main__":
    main()
