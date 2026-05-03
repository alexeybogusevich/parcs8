import asyncio
from argparse import ArgumentParser
from uuid import uuid4

from rich.console import Console
from rich.live import Live

from mcp_eval.agent import create_parcs_agent
from mcp_eval.console import AgentDisplay


DEFAULT_TASK = "You have a portfolio of 50 assets with equal weights (w_i = 1/50). The return covariance matrix Σ ∈ ℝ^{50×50} is generated from seed=42: A = randn(50,50,seed=42); Σ = Aᵀ·A/50 + 0.01·I. Using 2,000,000 Monte Carlo scenarios (each of 20 workers generates 100,000 using seed WorkerIndex*1000+42 and the shared Cholesky factor L of Σ), estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of the portfolio loss distribution. Loss = –(portfolio return). Return {var_99, cvar_99} as floats."


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

    thread_id = uuid4()

    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", task)]},
            config={"configurable": {"thread_id": str(thread_id)}},
            stream_mode="values",
        ):
            messages = chunk.get("messages") or []
            new_messages = messages[display.printed_count :]
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

    self_reflection_task = "Now, reflect on the job you've just completed and find important insights that will help you to perform better in the future. Modify your memory: /skills/parcs-cluster/SKILL.md and /memory/AGENTS.md file."

    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", self_reflection_task)]},
            config={"configurable": {"thread_id": str(thread_id)}},
            stream_mode="values",
        ):
            messages = chunk.get("messages") or []
            new_messages = messages[display.printed_count :]
            if not new_messages:
                continue

            live.stop()
            for msg in new_messages:
                display.print_message(msg)
            display.printed_count = len(messages)
            live.update(display.spinner)
            live.start()

    console.print()
    console.print("[bold green]✓ Reflection Complete![/]")


if __name__ == "__main__":
    asyncio.run(main())
