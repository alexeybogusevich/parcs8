"""Task families. N is the forced number of plan layers (not counting the final reduce layer).

Both families need exactly N + 1 layers. The text fixes N, the layer plan and the output format;
everything else is identical across N, arms and models. Ground truth: scripts/ground_truth.py.
"""

import math
import re
from dataclasses import dataclass

N_GRID = (2, 4, 8, 16, 32)

# F1: integral of f over [A, B], one layer per subinterval, then a reduce layer.
F1_A, F1_B = 0.0, 8.0
F1_PANELS = 10_000
F1_ABS_TOL = 1e-9

# F2: K-th smallest of M hashed values by bisection, one layer per step, then a final layer.
F2_M = 16_777_216
F2_K = 5_000_000
F2_P = 2_147_483_647

F1_TEMPLATE = """\
Compute I = ∫ from 0 to 8 of f(x) dx on the PARCS cluster, where

    f(x) = exp(-x / 4) * cos(x + 0.5 * sin(2 * x))

Follow this layer plan exactly; it is part of the task:

1. Split [0, 8] into N = {n} equal subintervals [a_k, b_k] with a_k = 8 * k / {n} and
   b_k = 8 * (k + 1) / {n}, for k = 0, 1, ..., {n_last}.
2. Run every subinterval as its own layer with parallelism 1: {n} subinterval layers in total.
   Do not compute several subintervals in one layer.
3. Each subinterval layer integrates f over its [a_k, b_k] with composite Simpson's rule using
   {panels} panels (accurate to about 1e-12).
4. Then run one reduce layer with parallelism 1 whose previousLayerIds lists all {n} subinterval
   layers. It adds up their results and outputs I. The reduce layer has {n} parents: read parent j
   with input.GetPreviousOutputs(j) for j = 0, 1, ..., {n_last} (GetPreviousOutputs() alone reads
   only the first parent).

The run uses exactly {layers} layers. Report I from the reduce layer's output with at least
12 significant digits. End your answer with a line of the form

ANSWER: <number>
"""

F2_TEMPLATE = """\
Find, on the PARCS cluster, the midpoint that exactly N = {n} bisection steps reach when searching
for the K-th smallest of M numbers v_1, ..., v_M, where M = {m} and K = {k}.

The numbers: for i = 1, 2, ..., M, with unsigned 64-bit wrapping arithmetic (C# `ulong`),

    z = (ulong)i * 0x9E3779B97F4A7C15
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9
    z = (z ^ (z >> 27)) * 0x94D049BB133111EB
    z = z ^ (z >> 31)
    v_i = (double)(z % {p}) / {p}.0

The bisection: start with lo = 0, hi = 1. One step computes mid = (lo + hi) / 2 and
c = the number of i with v_i <= mid; then, if c >= K, hi = mid, otherwise lo = mid.

Follow this layer plan exactly; it is part of the task:

1. Each step is one layer that counts in parallel: parallelism = maxParallelism from
   get_cluster_info, each worker counting its share of i = 1..M. That is {n} step layers in total.
   Do not fold several steps into one layer.
2. The layer for step j >= 2 lists the layer of step j - 1 in previousLayerIds. Its workers read
   lo, hi and the partial counts of step j - 1 from there, add the counts and update lo and hi
   themselves. Do not do this arithmetic yourself.
3. After step {n}, run one layer with parallelism 1 that reads the step-{n} layer the same way,
   applies the last update and outputs the final midpoint (lo + hi) / 2.

The run uses exactly {layers} layers. Report the final midpoint from the last layer's output with
17 significant digits. End your answer with a line of the form

ANSWER: <number>
"""


@dataclass(frozen=True)
class Task:
    family: str
    n: int
    text: str
    expected_layers: int


def render(family: str, n: int) -> Task:
    if n < 1:
        raise ValueError(f"n must be positive, got {n}")
    if family == "f1":
        text = F1_TEMPLATE.format(n=n, n_last=n - 1, panels=F1_PANELS, layers=n + 1)
    elif family == "f2":
        text = F2_TEMPLATE.format(n=n, m=F2_M, k=F2_K, p=F2_P, layers=n + 1)
    else:
        raise ValueError(f"unknown family {family!r}")
    return Task(family, n, text, n + 1)


def f2_tolerance(n: int) -> float:
    """A quarter of the distance between the midpoints after n and n + 1 steps."""
    return 2.0 ** -(n + 2) / 4


def tolerance(family: str, n: int) -> float:
    return F1_ABS_TOL if family == "f1" else f2_tolerance(n)


_NUMBER = r"([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)"
# `ANSWER: 0.25`, also lowercase, in Markdown emphasis or backticks, or as `ANSWER: I = 0.25`.
_ANSWER = re.compile(
    r"^[\s>*_`#-]*answer[\s*_`]*:[\s*_`]*(?:[A-Za-z_][\w()]*\s*=\s*)?[\s*_`]*" + _NUMBER,
    re.MULTILINE | re.IGNORECASE,
)


def parse_answer(text: str) -> float | None:
    """The number on the last `ANSWER:` line, or None."""
    matches = _ANSWER.findall(text or "")
    if not matches:
        return None
    value = float(matches[-1])
    return value if math.isfinite(value) else None
