"""The manifest: everything frozen for a sweep (backlog: Freeze rules). Each run record carries its hash."""

from __future__ import annotations

import hashlib
import json
import subprocess
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

from parcs_agent import prompts

from .cluster import pool
from .config import ROOT, Config
from .tasks import render, supports

# The runner is not tracked by git, so the git SHA alone does not pin it. Only what shapes a run is
# hashed: grading is left out (transcripts are complete, so runs can be regraded), and so are scripts.
RUN_PATH = ("agent.py", "cluster.py", "config.py", "manifest.py", "plan.py", "runner.py", "tasks.py")
CODE_FILES = (*(f"src/tool_calling_vs_ptc/{m}" for m in RUN_PATH), "tasks/ground_truth.json")
# Installed versions of the packages that run the agent: a `uv sync` must not change them unnoticed.
PACKAGES = (
    "parcs-agent",
    "eval-utils",
    "langchain",
    "langchain-core",
    "langgraph",
    "langchain-mcp-adapters",
    "langchain-quickjs",
    "quickjs-rs",
    "langchain-openai",
    "langchain-anthropic",
    "langchain-google-genai",
    "langchain-google-vertexai",
    "mcp",
)


def _sha(text: str | bytes) -> str:
    data = text.encode("utf-8") if isinstance(text, str) else text
    return hashlib.sha256(data).hexdigest()


def _git() -> dict:
    def run(*args: str) -> str:
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()

    try:
        dirty = bool(run("status", "--porcelain", "--untracked-files=no"))
        return {"sha": run("rev-parse", "HEAD"), "dirty_tracked": dirty}
    except (OSError, subprocess.CalledProcessError) as ex:
        return {"error": str(ex)}


def code_hash() -> str:
    h = hashlib.sha256()
    for name in CODE_FILES:
        h.update(name.encode())
        h.update((ROOT / name).read_bytes().replace(b"\r\n", b"\n"))
    return h.hexdigest()


def package_versions() -> dict[str, str | None]:
    def installed(name: str) -> str | None:
        try:
            return version(name)
        except PackageNotFoundError:
            return None

    return {name: installed(name) for name in PACKAGES}


def build(config: Config, cluster_info: dict) -> dict:
    exp = config.experiment
    return {
        "git": _git(),
        "code_sha256": code_hash(),
        "packages": package_versions(),
        "prompts_sha256": _sha(prompts.SHARED + prompts.TOOL_CALLING + prompts.PTC),
        "models": [m.public(config.llm) for m in config.models()],
        "ptc_options": exp.ptc_options(),
        "recursion_limit": exp.recursion_limit,
        "wall_budget_seconds": exp.wall_budget_seconds,
        "pool": pool(cluster_info),
        "tasks_sha256": {
            f"{f}-n{n}": _sha(render(f, n).text) for f in exp.families for n in exp.n_grid if supports(f, n)
        },
        "plan": {"families": exp.families, "n_grid": exp.n_grid, "arms": exp.arms, "reps": exp.reps, "seed": exp.seed},
    }


def manifest_hash(manifest: dict) -> str:
    return _sha(json.dumps(manifest, sort_keys=True))[:12]


def ensure(path: Path, manifest: dict) -> str:
    """Write the manifest on the first run of a results directory; afterwards refuse any change."""
    digest = manifest_hash(manifest)
    if path.exists():
        stored = json.loads(path.read_text(encoding="utf-8"))
        if stored.get("hash") != digest:
            changed = sorted(k for k in manifest if stored["manifest"].get(k) != manifest[k])
            raise SystemExit(
                f"{path} was frozen with hash {stored.get('hash')}, now {digest}; changed: {changed}. "
                "Use a new results directory, or delete the manifest if nothing has run yet."
            )
        return digest
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"hash": digest, "manifest": manifest}, indent=2) + "\n", encoding="utf-8")
    return digest
