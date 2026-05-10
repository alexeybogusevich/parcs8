from enum import StrEnum

from langchain.tools import BaseTool
from langchain_mcp_adapters.client import MultiServerMCPClient

from .config import config
from langchain_mcp_adapters.callbacks import Callbacks, CallbackContext

class ParcsMCPToolNames(StrEnum):
    """Enum for tool names used in ParcsMCP"""

    RunLayer = "run_layer"
    ListSessions = "list_sessions"
    GetLayerResults = "get_layer_results"
    SubmitLayer = "submit_layer"
    CreateSession = "create_session"
    GetClusterInfo = "get_cluster_info"


async def on_progress(
    progress: float,
    total: float | None,
    message: str | None,
    context: CallbackContext,
):
    """Handle progress updates from MCP servers."""
    percent = (progress / total * 100) if total else progress
    tool_info = f" ({context.tool_name})" if context.tool_name else ""
    print(f"[{context.server_name}{tool_info}] Progress: {percent:.1f}% - {message}")


async def get_parcs_mcp_tools() -> list[BaseTool]:
    """Returns a list of tools for a cluster based on app config

    Returns:
        list[BaseTool]: list of tools loaded from the cluster MCP instance
    """
    if not config.mcp.cluster_url:
        raise ValueError("Cluster URL must be provided in the configuration")

    client = MultiServerMCPClient(
        {"parcs": {"transport": "sse", "url": config.mcp.cluster_url + "/sse" }}, # "sse_read_timeout": 1000
        callbacks=Callbacks(on_progress=on_progress),
    )

    return await client.get_tools()