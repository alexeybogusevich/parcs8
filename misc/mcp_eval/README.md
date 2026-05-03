# PARCS-Agent Benchmark — Evaluation Runner

Runs the 15-task PARCS-Agent benchmark against a live PARCS cluster, comparing
sequential (Python REPL) vs. parallel (PARCS cluster) agent performance across
two LLM models. Results are written to a shared CSV file.

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| Python | 3.12+ | https://www.python.org/downloads/ |
| uv | latest | https://docs.astral.sh/uv/getting-started/installation/ |
| Google Cloud CLI | latest | https://cloud.google.com/sdk/docs/install |
| Git | any | https://git-scm.com |

---

## 1. Clone the repo

```bash
git clone https://github.com/alexeybogusevych/parcs7.git
```

```bash
cd parcs7/misc/mcp_eval
```

---

## 2. Set up Google Cloud authentication

You need a Google account that has been added as an Editor on the `parcs-gcp`
project (ask Oleksii if you haven't been added yet).

```bash
gcloud auth login
```

```bash
gcloud auth application-default login
```

```bash
gcloud config set project parcs-gcp
```

Verify it worked:

```bash
gcloud config get-value project
```

Expected output: `parcs-gcp`

```bash
gcloud auth application-default print-access-token
```

Expected output: a long token string (not an error).

---

## 3. Install dependencies

```bash
uv sync
```

Activate the virtual environment.

On macOS / Linux:

```bash
source .venv/bin/activate
```

On Windows (PowerShell):

```powershell
.venv\Scripts\Activate.ps1
```

---

## 4. Configure the environment

```bash
cp .env.example .env
```

Open `.env` in any text editor and set the following values:

```
# The PARCS cluster MCP endpoint — ask Oleksii for the current IP
MCP__CLUSTER_URL=http://34.76.43.4:8080

# Google Cloud settings for Vertex AI
LLM__PROVIDER=vertexai
LLM__PROJECT=parcs-gcp
LLM__LOCATION=us-central1

# Models to evaluate
EVAL__MODELS=gemini-2.5-flash,gemini-2.5-pro

# Disable LangSmith tracing (no API key needed)
LANGSMITH__TRACING_V2=false
LANGSMITH__API_KEY=
```

Everything else can stay as the example defaults.

---

## 5. Dry run — verify the setup

```bash
python run_eval.py --dry-run
```

You should see a table listing 60 planned runs (15 tasks × 2 models ×
sequential + parallel). If it errors before the table appears, check:

- `gcloud auth application-default print-access-token` returns a token
- `.env` has `MCP__CLUSTER_URL` set
- The virtual environment is activated (`which python` or `where python`
  should point inside `.venv`)

---

## 6. Smoke test — one task first

Before running everything, verify the cluster and model are reachable with a
single fast task:

```bash
python run_eval.py --tasks 1 --modes parallel --models gemini-2.5-flash
```

Task 1 is Monte Carlo VaR — pure maths, no dataset download, finishes in about
2 minutes. Check `results/benchmark_results.csv` afterwards — it should contain
one row with a non-empty `elapsed_seconds` and `result_summary`.

---

## 7. Full benchmark run

```bash
python run_eval.py
```

This runs all 15 tasks × 2 models × 2 modes = 60 runs in sequence. Each run
takes 2–15 minutes depending on the task; the full benchmark takes several
hours. Leave it running — results are saved to CSV after every single run so
nothing is lost if it stops.

The script is **resumable**. If it crashes or you interrupt it, just run it
again:

```bash
python run_eval.py
```

It will skip every task already marked `completed` in the CSV and continue from
where it left off.

To wipe the results and start completely fresh:

```bash
python run_eval.py --reset
```

---

## Command reference

```
python run_eval.py [options]

  --tasks 1,3,5            Only run these task IDs (default: all 15)
  --models gemini-2.5-flash  Override the model list from .env
  --modes parallel         Only run one mode: sequential or parallel
                           (default: sequential,parallel)
  --results path.csv       Write to a different results file
  --no-skip                Re-run tasks even if already completed
  --reset                  Delete the results file and start fresh
  --dry-run                Show what would run without executing anything
```

---

## Results file

`results/benchmark_results.csv` — one row per (task, model, mode) run.

Key columns:

| Column | Description |
|---|---|
| `task_title` | Human-readable task name, e.g. "Monte Carlo VaR" |
| `task_domain` | Research domain, e.g. "Quantitative Finance" |
| `task_question` | Full prompt given to the agent |
| `task_reference_answer` | Ground-truth answer for accuracy comparison |
| `elapsed_seconds` | Pure compute time (PARCS `totalElapsedSeconds` or Python exec) |
| `wall_clock_seconds` | Total time including all LLM reasoning turns |
| `cold_start_first_layer_seconds` | KEDA pod spin-up + daemon connect for the first layer |
| `cold_start_subsequent_avg_seconds` | Warm-pod overhead for subsequent layers |
| `parallelism_used` | Number of parallel workers the agent requested |
| `workers_succeeded` / `workers_failed` | Per-run worker outcome counts |
| `worker_success_rate` | Fraction of workers that completed successfully |
| `load_imbalance_ratio` | max_worker_time / min_worker_time — 1.0 is perfect balance |
| `worker_timings_json` | Per-worker elapsed seconds, one array per layer |
| `llm_turns` | Number of LLM reasoning steps |
| `total_tokens` | Total tokens consumed by the LLM |
| `compilation_failures` | Number of C# compile retries before success |
| `result_summary` | Agent's one-line description of the computed result |
| `result_json` | Agent's full structured JSON output |
| `parcs_session_id` | For cross-referencing with cluster logs |

---

## Troubleshooting

**404 on Vertex AI models** — run this REST diagnostic to check access directly:

```bash
TOKEN=$(gcloud auth application-default print-access-token)
```

```bash
curl -s \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"contents":[{"parts":[{"text":"Hi"}]}]}' \
  "https://us-central1-aiplatform.googleapis.com/v1/projects/parcs-gcp/locations/us-central1/publishers/google/models/gemini-2.5-flash:generateContent"
```

If this returns a 404, the Vertex AI Generative AI terms need to be accepted
in the Cloud Console — ask Oleksii to do this on the project.

**PARCS cluster errors** — the cluster must be running. Check with Oleksii
that the MCP server at `MCP__CLUSTER_URL` is up.

**Python version errors** — confirm you are running 3.12+:

```bash
python --version
```

On some systems you may need `python3` instead of `python`.

**Windows execution policy error** — if the venv activation fails on Windows:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try again:

```powershell
.venv\Scripts\Activate.ps1
```
