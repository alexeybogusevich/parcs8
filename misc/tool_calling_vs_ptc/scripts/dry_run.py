"""Run hand-written reference solutions of both task families through MCP (no LLM), following each
task's layer plan exactly, and compare with tasks/ground_truth.json.

usage: python dry_run.py URL f1 --n 2 32
       python dry_run.py URL f2 --n 2 32
Each (family, n) appends one JSON line to --out.
"""

import argparse
import asyncio
import json
import sys
import time
from pathlib import Path

from cluster_probe import UNFINISHED, Probe, summarize

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE / "src"))
from tool_calling_vs_ptc import tasks  # noqa: E402

SOURCE = """
using System; using System.Collections.Generic; using System.Linq;
using System.Text.Json; using System.Threading; using System.Threading.Tasks;
using Parcs.Agent.Runtime;

public class Part { public int k { get; set; } public double value { get; set; } }
public class State { public double lo { get; set; } public double hi { get; set; } public long count { get; set; } }

public class Sub : IAgentComputation {
    static double F(double x) => Math.Exp(-x / 4) * Math.Cos(x + 0.5 * Math.Sin(2 * x));
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct) {
        int n = int.Parse(input.Parameters["n"]), k = int.Parse(input.Parameters["k"]);
        int panels = int.Parse(input.Parameters["panels"]);
        double a = 8.0 * k / n, b = 8.0 * (k + 1) / n, h = (b - a) / panels, s = F(a) + F(b);
        for (int i = 1; i < panels; i++) s += (i % 2 == 1 ? 4 : 2) * F(a + i * h);
        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new Part { k = k, value = s * h / 3 })));
    }
}

public class Sum : IAgentComputation {
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct) {
        var parts = new List<double>();
        for (int p = 0; p < input.PreviousLayerResults.Count; p++)
            foreach (var o in input.GetPreviousOutputs(p)) {
                if (o == null) return Task.FromResult(AgentLayerResult.Error("parent " + p + " has a failed worker"));
                parts.Add(JsonSerializer.Deserialize<Part>(o)!.value);
            }
        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new { total = parts.Sum(), parts = parts.Count })));
    }
}

public static class Bisect {
    // lo/hi after applying the previous step's update; [0, 1] for the first step.
    public static (double lo, double hi) Interval(AgentLayerInput input, long k) {
        if (input.PreviousLayerResults.Count == 0) return (0.0, 1.0);
        var outs = input.GetPreviousOutputs();
        if (outs.Count == 0 || outs.Any(o => o == null)) throw new InvalidOperationException("previous step incomplete");
        var states = outs.Select(o => JsonSerializer.Deserialize<State>(o!)!).ToList();
        double lo = states[0].lo, hi = states[0].hi, mid = (lo + hi) / 2;
        return states.Sum(s => s.count) >= k ? (lo, mid) : (mid, hi);
    }
}

public class Step : IAgentComputation {
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct) {
        long m = long.Parse(input.Parameters["m"]), k = long.Parse(input.Parameters["k"]);
        ulong p = ulong.Parse(input.Parameters["p"]);
        var (lo, hi) = Bisect.Interval(input, k);
        double mid = (lo + hi) / 2, pd = p;
        long c = 0;
        for (long i = 1 + input.WorkerIndex; i <= m; i += input.TotalWorkers) {
            ulong z = (ulong)i * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            if ((z % p) / pd <= mid) c++;
        }
        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new State { lo = lo, hi = hi, count = c })));
    }
}

public class Final : IAgentComputation {
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct) {
        var (lo, hi) = Bisect.Interval(input, long.Parse(input.Parameters["k"]));
        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new { mid = (lo + hi) / 2, lo, hi })));
    }
}
"""


class DryRun(Probe):
    async def layer(self, args):
        env, dt = await self.call("run_layer", {"waitSeconds": 200, **args})
        while env.get("status") in UNFINISHED:
            env, d = await self.call("wait_layer", {"layerId": env["layerId"], "waitSeconds": 200})
            dt += d
        if env.get("status") != "Completed":
            raise SystemExit(f"layer {args.get('layerKey')} ended {env.get('status')}: {env.get('error')}")
        return env, dt

    async def session(self):
        env, dt = await self.call("create_session", {"sourceCode": SOURCE})
        if not env.get("ok"):
            raise SystemExit(f"create_session failed: {json.dumps(env.get('error'))[:2000]}")
        return env["sessionId"], dt

    @staticmethod
    def output(env, worker=0):
        out = env.get("output")
        if isinstance(out, list):
            out = out[worker]
        if isinstance(out, dict) and "outputData" in out:
            out = out["outputData"]
        return json.loads(out) if isinstance(out, str) else out

    async def f1(self, n, run):
        sid, _ = await self.session()
        params = {"n": str(n), "panels": str(tasks.F1_PANELS)}
        subs = await asyncio.gather(
            *(
                self.layer(
                    {
                        "sessionId": sid,
                        "entryPoint": "Sub",
                        "parallelism": 1,
                        "layerKey": f"{run}-sub-{k}",
                        "parameters": {**params, "k": str(k)},
                    }
                )
                for k in range(n)
            )
        )
        red, _ = await self.layer(
            {
                "sessionId": sid,
                "entryPoint": "Sum",
                "parallelism": 1,
                "layerKey": f"{run}-sum",
                "previousLayerIds": [env["layerId"] for env, _ in subs],
            }
        )
        return self.output(red)["total"], [env for env, _ in subs] + [red]

    async def f2(self, n, run):
        sid, _ = await self.session()
        info, _ = await self.call("get_cluster_info")
        workers = info["output"]["maxParallelism"]
        params = {"m": str(tasks.F2_M), "k": str(tasks.F2_K), "p": str(tasks.F2_P)}
        envs, prev = [], []
        for j in range(1, n + 1):
            env, _ = await self.layer(
                {
                    "sessionId": sid,
                    "entryPoint": "Step",
                    "parallelism": workers,
                    "layerKey": f"{run}-step-{j}",
                    "parameters": params,
                    "previousLayerIds": prev,
                }
            )
            envs.append(env)
            prev = [env["layerId"]]
        fin, _ = await self.layer(
            {
                "sessionId": sid,
                "entryPoint": "Final",
                "parallelism": 1,
                "layerKey": f"{run}-final",
                "parameters": params,
                "previousLayerIds": prev,
            }
        )
        return self.output(fin)["mid"], envs + [fin]

    async def run(self, family, n):
        truth = json.loads((HERE / "tasks" / "ground_truth.json").read_text(encoding="utf-8"))[family]["per_n"][str(n)]
        run = f"dry-{family}-{n}-{int(time.time())}"
        t0 = time.perf_counter()
        answer, envs = await getattr(self, family)(n, run)
        layers = [e.get("layer") or {} for e in envs]
        err = abs(answer - truth["answer"])
        self.record(
            "dry_run",
            {
                "family": family,
                "n": n,
                "answer": answer,
                "truth": truth["answer"],
                "abs_error": err,
                "passed": err <= truth["tolerance"],
                "layers": len(envs),
                "expected_layers": n + 1,
                "total_s": time.perf_counter() - t0,
                "elapsed_s": summarize([layer["elapsedSeconds"] for layer in layers if layer.get("elapsedSeconds")]),
                "queued_s_sum": sum(layer.get("queuedSeconds") or 0 for layer in layers),
            },
        )


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("url")
    ap.add_argument("family", choices=["f1", "f2"])
    ap.add_argument("--n", type=int, nargs="+", default=[2])
    ap.add_argument("--out", type=Path, default=HERE / "results" / "dry_run.jsonl")
    args = ap.parse_args()
    dry = DryRun(args.url, args.out)
    for n in args.n:
        await dry.run(args.family, n)


if __name__ == "__main__":
    asyncio.run(main())
