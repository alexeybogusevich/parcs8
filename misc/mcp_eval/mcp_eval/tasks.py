# """Load benchmark tasks from the HuggingFace dataset."""

# from __future__ import annotations

# from dataclasses import dataclass, field

# HF_REPO = "parcs-benchmark/parcs-agent-benchmark"
# HF_BENCHMARK_FILE = (
#     "hf://datasets/parcs-benchmark/parcs-agent-benchmark/data/benchmark.jsonl"
# )

# # ── Task metadata (title + domain — not stored in the HF dataset) ─────────────

# TASK_META: dict[int, tuple[str, str]] = {
#     1:  ("Monte Carlo Value-at-Risk",                   "Quantitative Finance"),
#     2:  ("Barrier Option Price Surface",                "Derivatives Pricing"),
#     3:  ("Travelling Salesman — Simulated Annealing",   "Combinatorial Optimisation"),
#     4:  ("Epidemic SIR Parameter Sweep",                "Epidemiology"),
#     5:  ("Gradient Boosting Hyperparameter Search",     "Machine Learning"),
#     6:  ("Bootstrap Survival Analysis (Cox PH)",        "Biostatistics"),
#     7:  ("Protein Sequence All-vs-All Similarity",      "Bioinformatics"),
#     8:  ("Random Forest from Scratch",                  "Machine Learning"),
#     9:  ("Safe Prime Generation (Miller-Rabin)",        "Cryptography"),
#     10: ("Financial Stress Testing — Scenario Repricing","Banking / Risk Management"),
#     11: ("Drug-Like Molecule Virtual Screening",        "Drug Discovery"),
#     12: ("Insurance Collective Risk — Ruin Probability","Actuarial Science"),
#     13: ("Large-Scale Graph Betweenness Centrality",    "Network Science"),
#     14: ("Monte Carlo Radiative Transfer",              "Atmospheric Science"),
#     15: ("Parallel Genome k-mer Frequency Spectrum",   "Bioinformatics / Genomics"),
# }


# @dataclass
# class BenchmarkTask:
#     task_id:  int
#     question: str
#     answer:   str          # JSON string with reference values
#     title:    str = field(default="")
#     domain:   str = field(default="")

#     def __post_init__(self) -> None:
#         if not self.title:
#             self.title, self.domain = TASK_META.get(self.task_id, ("", ""))


# def load_tasks(task_ids: list[int] | None = None) -> list[BenchmarkTask]:
#     from datasets import load_dataset  # type: ignore

#     ds = load_dataset("json", data_files=HF_BENCHMARK_FILE, split="train")

#     tasks: list[BenchmarkTask] = []
#     for row in ds:
#         tid = int(row["task_id"])
#         if task_ids and tid not in task_ids:
#             continue
#         tasks.append(BenchmarkTask(
#             task_id=tid,
#             question=str(row["question"]),
#             answer=str(row["answer"]),
#         ))

#     tasks.sort(key=lambda t: t.task_id)
#     return tasks


# # ── Prompt suffixes ────────────────────────────────────────────────────────────

# PARALLEL_PROMPT_SUFFIX = """
# ---
# **Evaluation instructions (follow exactly):**

# 1. Call `get_cluster_info` to find `maxParallelism`.
# 2. Write your C# computation, compile it with `create_session`.
# 3. Run `run_layer` with **parallelism=<maxParallelism>**.
# 4. If the task needs a final aggregation layer, run it with parallelism=1 and
#    add its `totalElapsedSeconds` to the total.
# 5. The parallel time is the sum of all `totalElapsedSeconds` values from
#    your `run_layer` calls.

# When finished, output a fenced JSON block — nothing after it:

# ```json
# {
#   "elapsed_seconds": <float, sum of all totalElapsedSeconds>,
#   "parallelism_used": <int>,
#   "result_summary": "<one sentence describing the key numerical result>"
# }
# ```
# """

# SEQUENTIAL_PROMPT_SUFFIX = """
# ---
# **Evaluation instructions (follow exactly):**

# 1. Implement the algorithm in Python using numpy / scipy / standard library.
# 2. Run it with `python_exec`. The tool returns `elapsed_seconds` in its output.
# 3. If the computation is slow, print `elapsed_seconds: X` explicitly at the
#    end of your script.

# When finished, output a fenced JSON block — nothing after it:

# ```json
# {
#   "elapsed_seconds": <float>,
#   "result_summary": "<one sentence describing the key numerical result>"
# }
# ```
# """


# def make_parallel_prompt(task: BenchmarkTask) -> str:
#     return task.question + PARALLEL_PROMPT_SUFFIX


# def make_sequential_prompt(task: BenchmarkTask) -> str:
#     return task.question + SEQUENTIAL_PROMPT_SUFFIX
