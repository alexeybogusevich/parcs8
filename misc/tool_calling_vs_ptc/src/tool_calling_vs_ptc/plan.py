"""The run plan: cells family × N × model × rep × arm, in an order that keeps drift off the arm axis."""

from __future__ import annotations

import json
import random
from dataclasses import asdict, dataclass
from pathlib import Path

from .tasks import supports

# Termination reasons that say nothing about the model: such a run is repeated on the next pass.
RETRYABLE = frozenset({"infra_error"})


@dataclass(frozen=True)
class Cell:
    family: str
    n: int
    arm: str
    model: str
    rep: int

    @property
    def run_id(self) -> str:
        return f"{self.family}-n{self.n}-{self.arm}-{self.model}-r{self.rep}"

    def as_dict(self) -> dict:
        return {**asdict(self), "run_id": self.run_id}


def ordered_cells(families, n_grid, arms, models, reps: int, seed: int) -> list[Cell]:
    """All cells. The (family, N, model, rep) blocks are shuffled; within a block the arms run back to
    back in a random order, so both arms of a block see the same cluster and provider conditions."""
    rng = random.Random(seed)
    blocks = [
        (f, n, m, r) for f in families for n in n_grid if supports(f, n) for m in models for r in range(1, reps + 1)
    ]
    rng.shuffle(blocks)
    cells = []
    for f, n, m, r in blocks:
        block_arms = list(arms)
        rng.shuffle(block_arms)
        cells += [Cell(f, n, a, m, r) for a in block_arms]
    return cells


def load_records(path: Path) -> dict[str, dict]:
    """The latest record of each run id."""
    records: dict[str, dict] = {}
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                record = json.loads(line)
                records[record["run_id"]] = record
    return records


def pending(cells: list[Cell], records: dict[str, dict]) -> list[Cell]:
    """Cells without a record, or whose latest record ended for an infrastructure reason."""
    return [c for c in cells if c.run_id not in records or records[c.run_id].get("termination") in RETRYABLE]


def append_record(path: Path, record: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record, default=str) + "\n")
