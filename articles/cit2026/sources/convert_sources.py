"""Render source code files in this directory to PNG using Pygments."""

from __future__ import annotations

from pathlib import Path

from pygments import highlight
from pygments.formatters import ImageFormatter
from pygments.lexers import get_lexer_for_filename
from pygments.util import ClassNotFound

SOURCES_DIR = Path(__file__).resolve().parent

# File extensions we want to render. The Pygments lexer is auto-detected.
SUPPORTED_SUFFIXES = {
    ".cs", ".py", ".ts", ".tsx", ".js", ".jsx",
    ".java", ".go", ".rs", ".cpp", ".c", ".h",
    ".sql", ".sh", ".ps1", ".yaml", ".yml", ".json", ".xml",
}


def render(path: Path) -> Path:
    code = path.read_text(encoding="utf-8")
    try:
        lexer = get_lexer_for_filename(path.name, code)
    except ClassNotFound:
        from pygments.lexers import guess_lexer
        lexer = guess_lexer(code)

    formatter = ImageFormatter(
        font_name="Consolas",
        font_size=18,
        line_numbers=True,
        line_number_bg="#f0f0f0",
        line_number_fg="#888888",
        line_number_separator=True,
        line_pad=4,
        image_pad=14,
        style="default",
    )

    out = path.with_suffix(path.suffix + ".png")
    out.write_bytes(highlight(code, lexer, formatter))
    return out


def main() -> None:
    self_path = Path(__file__).resolve()
    files = sorted(
        p for p in SOURCES_DIR.iterdir()
        if p.is_file()
        and p.suffix.lower() in SUPPORTED_SUFFIXES
        and p.resolve() != self_path
    )
    if not files:
        print(f"No source files found in {SOURCES_DIR}")
        return

    for path in files:
        out = render(path)
        print(f"{path.name} -> {out.name}")


if __name__ == "__main__":
    main()
