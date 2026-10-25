"""The runner's own MCP client: cluster state before a run, the server's layer list after it."""

from __future__ import annotations

import asyncio
import json
import time
from typing import Any

import httpx
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

TERMINAL = ("Completed", "CompletedWithFailures", "Failed", "Cancelled")
POOL_FIELDS = ("scalingMode", "workerNodeCount", "currentCapacity", "maxParallelism", "daemonCpuRequestMillicores")


def envelope(result: Any) -> dict:
    if getattr(result, "structuredContent", None) is not None:
        return result.structuredContent
    return json.loads(result.content[0].text)


class Cluster:
    def __init__(self, url: str):
        self.url = url.rstrip("/") + "/"

    async def call(self, name: str, args: dict | None = None) -> dict:
        async with (
            httpx.AsyncClient(timeout=httpx.Timeout(120.0)) as http,
            streamable_http_client(self.url, http_client=http) as (read, write, _),
            ClientSession(read, write) as session,
        ):
            await session.initialize()
            return envelope(await session.call_tool(name, args or {}))

    async def info(self) -> dict:
        env = await self.call("get_cluster_info")
        if not env.get("ok"):
            raise RuntimeError(f"get_cluster_info failed: {env.get('error')}")
        return env["output"]

    async def wait_idle(self, timeout: float, poll: float = 5.0) -> dict:
        """The cluster info once no layer is running or queued."""
        deadline = time.monotonic() + timeout
        while True:
            info = await self.info()
            if not info.get("runningLayers") and not info.get("queuedLayers"):
                return info
            if time.monotonic() >= deadline:
                raise TimeoutError(f"cluster still busy after {timeout:.0f} s: {info}")
            await asyncio.sleep(poll)

    async def layers(self, session_ids: list[str]) -> list[dict]:
        """Every layer of the given sessions, as the server lists them (with ``sessionId`` added)."""
        rows = []
        for sid in session_ids:
            env = await self.call("list_layers", {"sessionId": sid})
            if not env.get("ok"):
                raise RuntimeError(f"list_layers({sid}) failed: {env.get('error')}")
            rows += [{"sessionId": sid, **layer} for layer in env["output"]["layers"]]
        return rows

    async def cancel_unfinished(self, layers: list[dict]) -> list[str]:
        cancelled = []
        for layer in layers:
            if layer.get("status") not in TERMINAL:
                await self.call("cancel_layer", {"layerId": layer["layerId"]})
                cancelled.append(layer["layerId"])
        return cancelled


def pool(info: dict) -> dict:
    """The frozen part of the cluster info."""
    return {k: info.get(k) for k in POOL_FIELDS}
