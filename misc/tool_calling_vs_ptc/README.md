# Tool calling vs PTC on PARCS

Runs the same PARCS tasks through two agent arms built with [`parcs_agent`](../parcs_agent), and
sweeps the size N of the layer plan the task prescribes:

| Arm | The model... |
|---|---|
| `tool_calling` | calls the PARCS tools itself, turn by turn (parallel tool calls allowed) |
| `ptc` | writes JavaScript for a QuickJS `eval` tool that calls them as `tools.runLayer({...})` |

Task families (`src/tool_calling_vs_ptc/tasks.py`), each exactly N + 1 layers:

- **f1**, fan-out: an integral over N subintervals, one layer each, plus a reduce layer.
- **f2**, sequential: N bisection steps for the K-th smallest of 2²⁴ hashed values. Each step is one
  layer that reads the previous step's layer.

## Setup

```bash
uv sync
cp .env.example .env   # MCP__CLUSTER_URL, LLM__*, LANGSMITH__*, EXPERIMENT__MODELS
uv run --no-project --with numpy --with mpmath python scripts/ground_truth.py   # tasks/ground_truth.json
```

## Models

`EXPERIMENT__MODELS` lists the models (see `.env.example`). For an OpenRouter model,
`openrouter_provider` pins every call to one endpoint tag with fallbacks off, so one host,
quantization and price serve the whole sweep. A pin the account cannot use fails the first call with
a 404, and the sweep aborts. `reasoning_effort` is sent as OpenRouter's `reasoning.effort`. Both
fields are recorded in the manifest, with an unset effort recorded as `"default"`.

## Run

```bash
uv run tcvp plan                                         # the cells, in run order
uv run tcvp run --results results/pilot --n 2 32 --reps 1
uv run tcvp run                                          # the sweep (EXPERIMENT__*), resumable
uv run tcvp status --results results/pilot
```

`--families`, `--n`, `--arms`, `--models` and `--reps` override `EXPERIMENT__*`. Each results directory
has its own `manifest.json`, written on its first run. That run freezes the code, prompts, models,
limits, pool and task texts. A later run with any change is refused, so use a new directory instead.

Runs are sequential and wait for an idle cluster. Each run appends a record to `runs.jsonl` and
writes its full transcript to `transcripts/<run_id>.json`:

- messages, with usage and response metadata;
- every PARCS tool call, including the ones made inside `eval`;
- model-call times;
- the server's layer list;
- the grade.

A rerun skips cells that already have a record, except those that ended in `infra_error`.
`infra_error` covers transport and provider failures (rate limits and 5xx included, matched by
exception class, so langchain-openai's wrapped errors count), a run with no model reply, and a run
whose wall budget ran out while a PARCS call sat unanswered past its own `waitSeconds` plus a grace
period. A run that runs out of budget while the model is still working ends as `wall_budget`. Each
model call is logged when it starts, with the number of streamed chunks that arrived, so a call cut
off by the budget shows whether the model was generating or the provider had stalled.

Grading (`grading.py`): `success` means the answer is correct **and** came from the prescribed plan.
The layer whose output is the answer, together with its ancestors, must form exactly the N + 1 layer
plan. Any other layers are counted, not failed.

## Metrics

```bash
uv run tcvp-metrics results/pilot     # metrics.jsonl + metrics.csv, and a per-cell median summary
uv run tcvp-metrics --exploratory     # refetch the exploratory traces (tests/fixtures) and compare
```

`src/tool_calling_vs_ptc/analysis` regrades every run from its transcript, so grading fixes reach
runs already collected. `metrics.jsonl` is the source for analysis, and `runs.jsonl` only drives
resume. The package is not part of the manifest hash, so it can change during a sweep.

Costs come from token counts and the provider's per-token prices, with cache reads and writes priced
separately. The prices are snapshotted in the results directory's `pricing.json`. A pinned model is
priced at its endpoint, including the endpoint's discount (for example 0.51 on `deepinfra/fp8`).

Runs do not see each other on the shared server. Each run compiles its own session, and the
interceptor narrows `list_sessions` to the run's own sessions. A call naming another run's session or
layer gets the server's not-found error.

## Scripts

- `scripts/cluster_probe.py`: capacity, per-layer fixed cost and call limits (no LLM).
- `scripts/dry_run.py`: hand-written reference C# for both families, run through MCP (no LLM).
- `scripts/ground_truth.py`: offline ground truth.

```bash
uv run pytest
uv run ruff check . && uv run ruff format --check .
```
