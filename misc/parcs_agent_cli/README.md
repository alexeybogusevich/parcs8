# PARCS Agent CLI

Runs one task through an agent connected to a live PARCS cluster and traces the run in LangSmith.
The agent is built with [`parcs_agent`](../parcs_agent) (middleware over the cluster's MCP server) and
configured with [`eval_utils`](../eval_utils) (settings and model factory).

## Setup

Requires Python 3.12+ and [uv](https://docs.astral.sh/uv/). For Vertex AI, run
`gcloud auth application-default login` first.

```bash
uv sync
cp .env.example .env   # set MCP__CLUSTER_URL, the LLM and LANGSMITH__* values
```

## Run

```bash
uv run python main.py --task "..."
uv run python main.py --mode ptc --task "..."
```

| `--mode` | The model... |
|---|---|
| `tool_calling` (default) | calls the PARCS tools itself, one tool call per step |
| `ptc` | writes JavaScript for a QuickJS `eval` tool that calls them as `tools.runLayer({...})` |

`AGENT__MODE`, `AGENT__EVAL_TIMEOUT_SECONDS`, `AGENT__MAX_PTC_CALLS` and `AGENT__MAX_RESULT_CHARS` in `.env`
set the defaults. With `LANGSMITH__TRACING_V2=true` each run is traced to `LANGSMITH__PROJECT`, tagged with
its `thread_id`, `mode` and `model`; each PARCS call is one tool run, nested under `eval` in `ptc` mode.
