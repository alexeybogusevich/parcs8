"""Task families. The text fixes N, the plan and the output format; everything else is identical across
N, arms and models. Ground truth: scripts/ground_truth.py.

Two problems, each planned two ways: every step a layer the agent starts, or the steps inside the cluster.

- f1: the integral, N subinterval layers and one reduce layer (N + 1 layers).
- f3: the same integral reduced in a binary tree of layers (2N - 1 layers, log2 N levels).
- f3s: the same tree as one layer whose points spawn their children (1 layer, 2N - 1 points).
- f2: bisection, one layer per step and a final layer (N + 1 layers).
- f2p: the same bisection in one layer whose workers add up their counts over peer channels (1 layer).
"""

import math
import re
from dataclasses import dataclass

N_GRID = (2, 4, 8, 16, 32)
FAMILIES = ("f1", "f2", "f3", "f3s", "f2p")
# f3s uses 2N - 1 points in one layer; a static pool allows 4 x maxParallelism (16) points per layer.
N_MAX = {"f3s": 8}
# Families whose grading needs the layers' peerChannels and spawnedPoints, which list_layers leaves out.
LAYER_DETAILS = frozenset({"f3s", "f2p"})


def supports(family: str, n: int) -> bool:
    return n <= N_MAX.get(family, max(N_GRID))


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


F3_TEMPLATE = """\
Compute I = ∫ from 0 to 8 of f(x) dx on the PARCS cluster, where

    f(x) = exp(-x / 4) * cos(x + 0.5 * sin(2 * x))

Follow this layer plan exactly; it is part of the task:

1. Split [0, 8] into N = {n} equal subintervals [a_k, b_k] with a_k = 8 * k / {n} and
   b_k = 8 * (k + 1) / {n}, for k = 0, 1, ..., {n_last}.
2. Run every subinterval as its own leaf layer with parallelism 1: {n} leaf layers in total.
   Each leaf layer integrates f over its [a_k, b_k] with composite Simpson's rule using {panels}
   panels (accurate to about 1e-12).
3. Add the leaf results up in a binary tree of combine layers, {levels} levels deep. A combine
   layer has parallelism 1 and exactly two parents, previousLayerIds = [left, right]; it reads
   them with input.GetPreviousOutputs(0) and input.GetPreviousOutputs(1) and outputs their sum.
   Level 1 combines leaves 2i and 2i + 1; every further level combines layers 2i and 2i + 1 of
   the level below, until one combine layer, the root, is left: {combines} combine layers in total.
   Never combine more than two layers in one layer.

The run uses exactly {layers} layers. Report I from the root layer's output with at least
12 significant digits. End your answer with a line of the form

ANSWER: <number>
"""

F3S_TEMPLATE = """\
Compute I = ∫ from 0 to 8 of f(x) dx on the PARCS cluster, where

    f(x) = exp(-x / 4) * cos(x + 0.5 * sin(2 * x))

Follow this plan exactly; it is part of the task:

1. Run exactly one layer, with parallelism 1, that computes I by recursive splitting: its points
   spawn their children themselves. Pass maxPoints = {points} and maxDepth = {levels} to run_layer.
2. Every point is responsible for one interval [a, b]; the layer's worker is the point at depth 0,
   responsible for [0, 8]. A point at depth d < {levels} splits [a, b] at its midpoint, spawns two
   child points for the two halves, waits for their results and outputs their sum. A point at
   depth {levels} integrates f over its [a, b] with composite Simpson's rule using {panels} panels
   (accurate to about 1e-12).
3. So the points at depth {levels} are the N = {n} equal subintervals [8 * k / {n}, 8 * (k + 1) / {n}],
   and the layer spawns {spawned} points in all. Do not run any other layer, and do not add
   anything up yourself.

The spawning API (Parcs.Agent.Runtime), inside ExecuteAsync:

    input.Depth            // 0 for the layer's worker, 1 for the points it spawned, and so on
    input.WorkerInput      // the string the spawning point passed to this point; null at depth 0
    IAgentSpawn spawn = await input.SpawnAsync(2, inputs: new[] {{ leftInput, rightInput }},
                                               cancellationToken: cancellationToken);
    IReadOnlyList<string?> results = await spawn.ResultsAsync(cancellationToken); // in order

Spawned points run the same class as the point that spawned them.

The run uses exactly 1 layer. Report I from the layer's output with at least 12 significant
digits. End your answer with a line of the form

ANSWER: <number>
"""

F2P_TEMPLATE = """\
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

Follow this plan exactly; it is part of the task:

1. Run exactly one layer that performs all {n} steps: parallelism = maxParallelism from
   get_cluster_info, each worker counting its share of i = 1..M at every step. Do not run a
   layer per step.
2. At every step the workers add up their partial counts over peer channels, so that each of
   them learns c and updates lo and hi itself. Do not do this arithmetic yourself.
3. After step {n}, worker 0 outputs the final midpoint (lo + hi) / 2.

The peer channel API (Parcs.Agent.Runtime), inside ExecuteAsync:

    input.WorkerIndex, input.TotalWorkers
    IAgentChannel channel = input.Peer(j);  // the channel to worker j, opened on first use
    await channel.SendAsync(text, cancellationToken);                // buffered, never blocks
    string message = await channel.ReceiveAsync(cancellationToken);  // worker j's next message
    // SendJsonAsync(value) and ReceiveJsonAsync<T>() do the same with JSON.

A receive waits at most the layer's channel timeout (run_layer's channelTimeoutSeconds, default
60 s) and then throws TimeoutException.

The run uses exactly 1 layer. Report the final midpoint from worker 0's output with 17 significant
digits. End your answer with a line of the form

ANSWER: <number>
"""


@dataclass(frozen=True)
class Task:
    family: str
    n: int
    text: str
    expected_layers: int


def levels(n: int) -> int:
    """Depth of the binary reduction tree over n leaves (f3, f3s); n must be a power of two."""
    if n < 2 or n & (n - 1):
        raise ValueError(f"the tree families need n to be a power of two >= 2, got {n}")
    return n.bit_length() - 1


def render(family: str, n: int) -> Task:
    if n < 1:
        raise ValueError(f"n must be positive, got {n}")
    if family not in FAMILIES:
        raise ValueError(f"unknown family {family!r}")
    if not supports(family, n):
        raise ValueError(f"{family} supports n <= {N_MAX[family]}, got {n}")
    if family == "f1":
        text, layers = F1_TEMPLATE.format(n=n, n_last=n - 1, panels=F1_PANELS, layers=n + 1), n + 1
    elif family == "f2":
        text, layers = F2_TEMPLATE.format(n=n, m=F2_M, k=F2_K, p=F2_P, layers=n + 1), n + 1
    elif family == "f3":
        layers = 2 * n - 1
        text = F3_TEMPLATE.format(n=n, n_last=n - 1, panels=F1_PANELS, levels=levels(n), combines=n - 1, layers=layers)
    elif family == "f3s":
        text, layers = (
            F3S_TEMPLATE.format(n=n, panels=F1_PANELS, levels=levels(n), points=2 * n - 1, spawned=2 * n - 2),
            1,
        )
    else:
        text, layers = F2P_TEMPLATE.format(n=n, m=F2_M, k=F2_K, p=F2_P), 1
    return Task(family, n, text, layers)


def problem(family: str) -> str:
    """The family whose problem, ground truth and tolerance this one shares."""
    return {"f3": "f1", "f3s": "f1", "f2p": "f2"}.get(family, family)


def f2_tolerance(n: int) -> float:
    """A quarter of the distance between the midpoints after n and n + 1 steps."""
    return 2.0 ** -(n + 2) / 4


def tolerance(family: str, n: int) -> float:
    return F1_ABS_TOL if problem(family) == "f1" else f2_tolerance(n)


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
