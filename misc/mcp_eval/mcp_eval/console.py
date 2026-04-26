"""Console rendering for the agent message stream."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from rich.console import Console
from rich.markdown import Markdown
from rich.panel import Panel
from rich.spinner import Spinner

from langchain_core.messages import AIMessage, BaseMessage, HumanMessage, ToolMessage

from .tools import ParcsMCPToolNames


THINKING_TEXT = "Thinking..."
SPINNER_STYLE = "dots"


@dataclass(frozen=True)
class ToolStyle:
    """Display metadata for a single tool kind."""

    label: str
    color: str
    arg_key: str | None = None

    def describe(self, args: dict[str, Any]) -> str:
        if self.arg_key and (value := args.get(self.arg_key)):
            return f"{self.label}: {value}"
        return self.label


TOOL_STYLES: dict[str, ToolStyle] = {
    ParcsMCPToolNames.RunLayer: ToolStyle("Running layer", "cyan"),
    ParcsMCPToolNames.CreateSession: ToolStyle("Creating session", "yellow"),
    ParcsMCPToolNames.GetClusterInfo: ToolStyle("Getting cluster info", "blue"),
    ParcsMCPToolNames.SubmitLayer: ToolStyle("Submitting layer", "green"),
    ParcsMCPToolNames.GetLayerResults: ToolStyle("Getting layer results", "green"),
    "read_file": ToolStyle("Reading", "magenta", arg_key="file_path"),
}


def _style_for(name: str) -> ToolStyle:
    return TOOL_STYLES.get(name, ToolStyle(f"Calling {name or 'tool'}", "white"))


def _extract_text(content: Any) -> str:
    """Flatten LangChain message content into a plain string."""
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(
            part.get("text", "")
            for part in content
            if isinstance(part, dict) and part.get("type") == "text"
        )
    return ""


def _tool_message_text(msg: ToolMessage) -> str:
    content = msg.content
    if isinstance(content, list) and content:
        first = content[0]
        if isinstance(first, dict):
            return str(first.get("text", ""))
    if isinstance(content, str):
        return content
    return ""


def _is_failure(msg: ToolMessage) -> bool:
    if getattr(msg, "status", None) == "error":
        return True
    return "compilation failed" in _tool_message_text(msg).lower()


class AgentDisplay:
    """Renders streamed agent messages and exposes a status spinner.

    The spinner shows what tool calls are currently in flight; it falls back
    to "Thinking..." once every issued tool call has produced a ToolMessage.
    """

    def __init__(self, console: Console) -> None:
        self.console = console
        self.printed_count = 0
        self._pending: dict[str, str] = {}
        self.spinner = Spinner(SPINNER_STYLE, text=THINKING_TEXT)

    def print_message(self, msg: BaseMessage) -> None:
        if isinstance(msg, HumanMessage):
            self._print_human(msg)
        elif isinstance(msg, AIMessage):
            self._print_ai(msg)
        elif isinstance(msg, ToolMessage):
            self._print_tool(msg)
        self._refresh_spinner()

    def _print_human(self, msg: HumanMessage) -> None:
        self.console.print(
            Panel(str(msg.content), title="You", border_style="blue")
        )

    def _print_ai(self, msg: AIMessage) -> None:
        text = _extract_text(msg.content)
        if text.strip():
            self.console.print(
                Panel(Markdown(text), title="Agent", border_style="green")
            )
        for tc in msg.tool_calls or []:
            self._announce_tool_call(tc)

    def _announce_tool_call(self, tc: dict[str, Any]) -> None:
        name = tc.get("name", "unknown")
        args = tc.get("args") or {}
        style = _style_for(name)
        description = style.describe(args)
        self.console.print(f"  [bold {style.color}]>> {description}...[/]")
        key = tc.get("id") or f"{name}:{len(self._pending)}"
        self._pending[key] = description

    def _print_tool(self, msg: ToolMessage) -> None:
        name = getattr(msg, "name", "") or ""
        style = _style_for(name)
        if _is_failure(msg):
            detail = _tool_message_text(msg).strip() or "see logs"
            self.console.print(
                f"  [red]✗ {style.label} failed: {detail}[/]"
            )
        else:
            self.console.print(f"  [green]✓ {style.label} done[/]")
        self._clear_pending(msg, name)

    def _clear_pending(self, msg: ToolMessage, name: str) -> None:
        tc_id = getattr(msg, "tool_call_id", None)
        if tc_id and tc_id in self._pending:
            self._pending.pop(tc_id)
            return
        # Fallback: clear the first pending entry that matches by name prefix.
        for key, label in list(self._pending.items()):
            if key.startswith(f"{name}:") or label.startswith(_style_for(name).label):
                self._pending.pop(key)
                return

    def _refresh_spinner(self) -> None:
        if not self._pending:
            text = THINKING_TEXT
        elif len(self._pending) == 1:
            text = next(iter(self._pending.values()))
        else:
            joined = ", ".join(self._pending.values())
            text = f"Running {len(self._pending)} tools: {joined}"
        self.spinner = Spinner(SPINNER_STYLE, text=text)
