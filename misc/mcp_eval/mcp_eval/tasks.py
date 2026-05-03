"""Load benchmark tasks from the HuggingFace dataset."""

from __future__ import annotations

from dataclasses import dataclass

HF_REPO = "parcs-benchmark/parcs-agent-benchmark"
# HuggingFace auto-assigned train/test splits from the task_08_* files,
# so the benchmark split must be loaded directly from its raw file path.
HF_BENCHMARK_FILE = (
    "hf://datasets/parcs-benchmark/parcs-agent-benchmark/data/benchmark.jsonl"
)


@dataclass
class BenchmarkTask:
    task_id: int
    question: str
    answer: str  # JSON string with reference values


def load_tasks(task_ids: list[int] | None = None) -> list[BenchmarkTask]:
    """Download the benchmark split and return the requested tasks.

    Args:
        task_ids: If provided, return only tasks with these IDs (1-15).
                  If None or empty, return all 15 tasks.
    """
    from datasets import load_dataset  # type: ignore

    # Load directly from the raw JSONL file; HuggingFace maps a single file
    # to the "train" split by default regardless of the filename.
    ds = load_dataset("json", data_files=HF_BENCHMARK_FILE, split="train")

    tasks: list[BenchmarkTask] = []
    for row in ds:
        tid = int(row["task_id"])
        if task_ids and tid not in task_ids:
            continue
        tasks.append(
            BenchmarkTask(
                task_id=tid,
                question=str(row["question"]),
                answer=str(row["answer"]),
            )
        )

    tasks.sort(key=lambda t: t.task_id)
    return tasks


PARALLEL_PROMPT_SUFFIX = """
---
**Evaluation instructions (follow exactly):**

1. Call `get_cluster_info` to find `maxParallelism`.
2. Write your C# computation, compile it with `create_session`.
3. Run `run_layer` with **parallelism=<maxParallelism>**.
4. If the task needs a final aggregation layer, run it with parallelism=1 and
   add its `totalElapsedSeconds` to the total.
5. The parallel time is the sum of all `totalElapsedSeconds` values from
   your `run_layer` calls.

When finished, output a fenced JSON block — nothing after it:

```json
{
  "elapsed_seconds": <float, sum of all totalElapsedSeconds>,
  "parallelism_used": <int>,
  "result_summary": "<one sentence describing the key numerical result>"
}
```
"""

SEQUENTIAL_PROMPT_SUFFIX = """
---
**Evaluation instructions (follow exactly):**

1. Implement the algorithm in Python using numpy / scipy / standard library.
2. Run it with `python_exec`. The tool returns `elapsed_seconds` in its output.
3. If the computation is slow, add `import time; t=time.time()` and print the
   elapsed time explicitly at the end of your script.

When finished, output a fenced JSON block — nothing after it:

```json
{
  "elapsed_seconds": <float>,
  "result_summary": "<one sentence describing the key numerical result>"
}
```
"""


def make_parallel_prompt(task: BenchmarkTask) -> str:
    return task.question + PARALLEL_PROMPT_SUFFIX


def make_sequential_prompt(task: BenchmarkTask) -> str:
    return task.question + SEQUENTIAL_PROMPT_SUFFIX
