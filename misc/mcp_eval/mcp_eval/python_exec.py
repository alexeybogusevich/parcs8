"""Python REPL tool used by the sequential (no-cluster) agent.

Each call writes the submitted code to a temp file and runs it in a
fresh subprocess, returning stdout, stderr, elapsed seconds, and
return code.  The subprocess runs with the same interpreter / venv as
the parent process, so numpy, scipy, scikit-learn, etc. are all
available as long as they are installed.
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import time
from pathlib import Path

from langchain.tools import tool


@tool
def python_exec(code: str) -> str:
    """Execute a block of Python code and return the output.

    The code runs in a subprocess with access to numpy, scipy,
    scikit-learn, and the standard library.  Always print your final
    answer to stdout so it appears in the returned output.
    The tool returns a text block containing:
      - stdout
      - stderr (if any)
      - elapsed_seconds
      - returncode
    """
    with tempfile.NamedTemporaryFile(
        mode="w", suffix=".py", delete=False, encoding="utf-8"
    ) as fh:
        fh.write(code)
        script_path = fh.name

    try:
        start = time.monotonic()
        proc = subprocess.run(
            [sys.executable, script_path],
            capture_output=True,
            text=True,
            timeout=600,  # 10-minute hard cap per code block
        )
        elapsed = round(time.monotonic() - start, 3)
    except subprocess.TimeoutExpired:
        Path(script_path).unlink(missing_ok=True)
        return "ERROR: script exceeded the 10-minute time limit."
    finally:
        Path(script_path).unlink(missing_ok=True)

    lines = [f"elapsed_seconds: {elapsed}", f"returncode: {proc.returncode}"]
    if proc.stdout:
        lines += ["--- stdout ---", proc.stdout.rstrip()]
    if proc.stderr:
        lines += ["--- stderr ---", proc.stderr.rstrip()]
    return "\n".join(lines)
