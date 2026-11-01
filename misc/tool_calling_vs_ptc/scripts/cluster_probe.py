"""Probe a PARCS MCP server directly (no LLM): capacity, per-layer fixed cost, queueing, call limits.

usage: python cluster_probe.py URL info
       python cluster_probe.py URL fixedcost --reps 20 --parallelism 1
       python cluster_probe.py URL concurrent --layers 16   # submit_layer one by one, then wait
       python cluster_probe.py URL gather --layers 32       # N run_layer calls at once, one MCP session each
       python cluster_probe.py URL sleep --seconds 70 --wait 90
Each mode appends one JSON line to --out.
"""

import argparse
import asyncio
import json
import statistics
import time
from pathlib import Path

import httpx
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

SOURCE = """
using System;
using System.Threading;
using System.Threading.Tasks;
using Parcs.Agent.Runtime;
public class Noop : IAgentComputation {
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct)
        => Task.FromResult(AgentLayerResult.Ok("{\\"w\\":" + input.WorkerIndex + "}"));
}
public class Sleep : IAgentComputation {
    public async Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct) {
        await Task.Delay(TimeSpan.FromSeconds(double.Parse(input.Parameters["seconds"])), ct);
        return AgentLayerResult.Ok("{\\"slept\\":true}");
    }
}
"""

UNFINISHED = ("Queued", "Running")


def envelope(result):
    if result.structuredContent is not None:
        return result.structuredContent
    return json.loads(result.content[0].text)


def summarize(xs):
    xs = sorted(xs)
    if not xs:
        return None
    q = statistics.quantiles(xs, n=4) if len(xs) >= 4 else [xs[0], statistics.median(xs), xs[-1]]
    return {"n": len(xs), "median": statistics.median(xs), "q1": q[0], "q3": q[2], "min": xs[0], "max": xs[-1]}


class Probe:
    def __init__(self, url: str, out: Path):
        self.url = url
        self.out = out

    def record(self, kind, payload):
        line = {"kind": kind, "at": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "url": self.url, **payload}
        with self.out.open("a", encoding="utf-8") as f:
            f.write(json.dumps(line) + "\n")
        print(json.dumps({k: v for k, v in line.items() if k != "rows"}, indent=2))

    async def call(self, name, args=None):
        """One tool call on its own MCP session, as langchain-mcp-adapters does per tool call."""
        # Generous client timeouts so that only the server or the network path can cut a call.
        async with (
            httpx.AsyncClient(timeout=httpx.Timeout(600.0)) as http,
            streamable_http_client(self.url, http_client=http) as (read, write, _),
            ClientSession(read, write) as session,
        ):
            await session.initialize()
            t0 = time.perf_counter()
            env = envelope(await session.call_tool(name, args or {}))
            return env, time.perf_counter() - t0

    async def session_id(self):
        env, dt = await self.call("create_session", {"sourceCode": SOURCE})
        self.record(
            "create_session",
            {"latency_s": dt, "status": env.get("status"), "ok": env.get("ok"), "error": env.get("error")},
        )
        if not env.get("ok"):
            raise SystemExit("create_session failed")
        return env["sessionId"]

    async def until_done(self, env, dt):
        while env.get("status") in UNFINISHED:
            env, d = await self.call("wait_layer", {"layerId": env["layerId"], "waitSeconds": 200})
            dt += d
        return env, dt

    async def info(self):
        env, dt = await self.call("get_cluster_info")
        self.record("cluster_info", {"latency_s": dt, "envelope": env})

    async def fixedcost(self, reps, parallelism):
        sid = await self.session_id()
        rows = []
        for i in range(reps):
            env, dt = await self.call(
                "run_layer", {"sessionId": sid, "entryPoint": "Noop", "parallelism": parallelism, "waitSeconds": 200}
            )
            env, dt = await self.until_done(env, dt)
            layer = env.get("layer") or {}
            rows.append(
                {
                    "i": i,
                    "client_s": dt,
                    "status": env.get("status"),
                    "queued_s": layer.get("queuedSeconds"),
                    "elapsed_s": layer.get("elapsedSeconds"),
                    "error": env.get("error"),
                }
            )
        ok = [r for r in rows if r["status"] == "Completed"]
        self.record(
            "fixedcost",
            {
                "parallelism": parallelism,
                "reps": reps,
                "completed": len(ok),
                "client_s": summarize([r["client_s"] for r in ok]),
                "elapsed_s": summarize([r["elapsed_s"] for r in ok if r["elapsed_s"] is not None]),
                "queued_s": summarize([r["queued_s"] for r in ok if r["queued_s"] is not None]),
                "rows": rows,
            },
        )

    async def concurrent(self, layers):
        sid = await self.session_id()
        t0 = time.perf_counter()
        subs = [
            (await self.call("submit_layer", {"sessionId": sid, "entryPoint": "Noop", "parallelism": 1}))[0]
            for _ in range(layers)
        ]
        submitted_s = time.perf_counter() - t0
        finals = [(await self.until_done(env, 0.0))[0] for env in subs]
        self.layer_summary("concurrent", layers, finals, time.perf_counter() - t0, {"submit_all_s": submitted_s})

    async def gather(self, layers, wait):
        sid = await self.session_id()
        args = {"sessionId": sid, "entryPoint": "Noop", "parallelism": 1}
        if wait is not None:
            args["waitSeconds"] = wait
        t0 = time.perf_counter()
        results = await asyncio.gather(*(self.call("run_layer", args) for _ in range(layers)))
        total_s = time.perf_counter() - t0
        envs = [env for env, _ in results]
        self.layer_summary(
            "gather",
            layers,
            envs,
            total_s,
            {
                "wait_seconds": wait,
                "first_response_statuses": [e.get("status") for e in envs],
                "call_s": summarize([dt for _, dt in results]),
            },
        )

    async def sleep(self, seconds, wait):
        sid = await self.session_id()
        t0 = time.perf_counter()
        try:
            env, dt = await self.call(
                "run_layer",
                {
                    "sessionId": sid,
                    "entryPoint": "Sleep",
                    "parallelism": 1,
                    "waitSeconds": wait,
                    "parameters": {"seconds": str(seconds)},
                },
            )
            layer = env.get("layer") or {}
            self.record(
                "sleep",
                {
                    "seconds": seconds,
                    "wait_seconds": wait,
                    "call_s": dt,
                    "status": env.get("status"),
                    "elapsed_s": layer.get("elapsedSeconds"),
                    "error": env.get("error"),
                },
            )
        except Exception as ex:  # a cut connection surfaces here
            self.record(
                "sleep",
                {
                    "seconds": seconds,
                    "wait_seconds": wait,
                    "call_s": time.perf_counter() - t0,
                    "status": "client_error",
                    "error": f"{type(ex).__name__}: {ex}",
                },
            )

    def layer_summary(self, kind, layers, envs, total_s, extra):
        info = [e.get("layer") or {} for e in envs]
        self.record(
            kind,
            {
                "layers": layers,
                "total_s": total_s,
                **extra,
                "statuses": [e.get("status") for e in envs],
                "queued_s": summarize([x["queuedSeconds"] for x in info if x.get("queuedSeconds") is not None]),
                "elapsed_s": summarize([x["elapsedSeconds"] for x in info if x.get("elapsedSeconds") is not None]),
                "errors": [e.get("error") for e in envs if e.get("error")],
            },
        )


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("url")
    ap.add_argument("what", choices=["info", "fixedcost", "concurrent", "gather", "sleep"])
    ap.add_argument("--reps", type=int, default=20)
    ap.add_argument("--parallelism", type=int, default=1)
    ap.add_argument("--layers", type=int, default=4)
    ap.add_argument("--seconds", type=float, default=70)
    ap.add_argument("--wait", type=int, default=None, help="waitSeconds; omitted = server default (60)")
    out = Path(__file__).resolve().parent.parent / "results" / "cluster_probe.jsonl"
    ap.add_argument("--out", type=Path, default=out)
    a = ap.parse_args()
    a.out.parent.mkdir(parents=True, exist_ok=True)
    probe = Probe(a.url, a.out)

    match a.what:
        case "info":
            await probe.info()
        case "fixedcost":
            await probe.fixedcost(a.reps, a.parallelism)
        case "concurrent":
            await probe.concurrent(a.layers)
        case "gather":
            await probe.gather(a.layers, a.wait)
        case "sleep":
            await probe.sleep(a.seconds, a.wait if a.wait is not None else 90)


if __name__ == "__main__":
    asyncio.run(main())
