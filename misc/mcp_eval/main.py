import asyncio
from argparse import ArgumentParser

from rich.console import Console
from rich.live import Live

from mcp_eval.agent import create_parcs_agent
from mcp_eval.console import AgentDisplay


DEFAULT_TASK = (
    "Write a report on the current status of the cluster and recent job results."
)


def get_task_from_args() -> str:
    parser = ArgumentParser(description="Run the Parcs MCP Agent")
    parser.add_argument(
        "--task",
        type=str,
        help="The task to be performed by the agent",
        default=DEFAULT_TASK,
    )
    return parser.parse_args().task


async def main() -> None:
    task = get_task_from_args()

    agent = await create_parcs_agent()
    console = Console()
    display = AgentDisplay(console)

    console.print()

    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", task)]},
            stream_mode="values",
        ):
            messages = chunk.get("messages") or []
            new_messages = messages[display.printed_count:]
            if not new_messages:
                continue

            live.stop()
            for msg in new_messages:
                display.print_message(msg)
            display.printed_count = len(messages)
            live.update(display.spinner)
            live.start()

    console.print()
    console.print("[bold green]✓ Done![/]")


if __name__ == "__main__":
    asyncio.run(main())
