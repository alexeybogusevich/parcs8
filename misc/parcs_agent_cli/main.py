import asyncio
from argparse import ArgumentParser
from uuid import uuid4

from rich.console import Console
from rich.live import Live

from parcs_agent_cli.agent import create_parcs_agent
from parcs_agent_cli.config import config
from parcs_agent_cli.console import AgentDisplay

DEFAULT_TASK = "You have a portfolio of 50 assets with equal weights (w_i = 1/50). The return covariance matrix Σ ∈ ℝ^{50×50} is generated from seed=42: A = randn(50,50,seed=42); Σ = Aᵀ·A/50 + 0.01·I. Using 2,000,000 Monte Carlo scenarios (each of 20 workers generates 100,000 using seed WorkerIndex*1000+42 and the shared Cholesky factor L of Σ), estimate the 1-day 99% Value-at-Risk (VaR) and Conditional VaR (CVaR) of the portfolio loss distribution. Loss = –(portfolio return). Return {var_99, cvar_99} as floats."


def parse_args():
    parser = ArgumentParser(description="Run the Parcs MCP Agent")
    parser.add_argument(
        "--task",
        type=str,
        help="The task to be performed by the agent",
        default=DEFAULT_TASK,
    )
    parser.add_argument(
        "--mode",
        choices=["tool_calling", "ptc"],
        default=None,
        help="tool_calling: the model calls PARCS tools turn by turn; ptc: it writes JavaScript that "
        "calls them (default: AGENT__MODE from .env, else tool_calling)",
    )
    return parser.parse_args()


async def stream(agent, text: str, run_config: dict, display: AgentDisplay, console: Console) -> list:
    messages: list = []
    with Live(
        display.spinner,
        console=console,
        refresh_per_second=10,
        transient=True,
    ) as live:
        async for chunk in agent.astream(
            {"messages": [("user", text)]},
            config=run_config,
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
    return messages


async def main() -> None:
    args = parse_args()
    mode = args.mode or config.agent.mode

    agent = await create_parcs_agent(mode)
    console = Console()
    display = AgentDisplay(console)

    console.print(f"[dim]mode={mode}  model={config.llm.model_name}[/]")
    console.print()

    # The metadata tags the LangSmith trace, where each PARCS call is one tool run (under `eval` in ptc).
    run_config = {
        "configurable": {"thread_id": str(uuid4())},
        "metadata": {"mode": mode, "model": config.llm.model_name},
    }
    await stream(agent, args.task, run_config, display, console)

    console.print()
    console.print("[bold green]✓ Done![/]")


if __name__ == "__main__":
    asyncio.run(main())
