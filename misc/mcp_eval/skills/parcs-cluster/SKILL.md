---
name: parcs-cluster
description: >
  Use this skill whenever the user wants to run distributed or parallel computation on the PARCS
  cluster. Triggers include: running parallel algorithms, distributing work across workers, executing
  C# code in parallel, using the PARCS MCP server, submitting compute jobs, running multi-layer
  pipelines, parallelising number crunching, Monte Carlo simulations, matrix operations, search
  problems, or any task where splitting work across many cores would speed things up. Also triggers
  when the user asks about cluster capacity, session management, or layer execution. Use this skill
  even if the user doesn't say "PARCS" explicitly — if they want to go fast with parallel C#, this
  is the right tool.
---

# PARCS Cluster — Distributed Parallel Compute via MCP

The PARCS cluster exposes a set of MCP tools that let you write and execute parallel C# code across
multiple worker pods in a GKE cluster. You submit C# source, PARCS compiles it with Roslyn, fans it
out to N daemon workers via Pub/Sub + KEDA, and returns aggregated results.

## Execution Model

A computation is a sequence of **layers**. Each layer fans out to N workers in parallel, waits for
all to complete, and returns the aggregated results. Workers in layer 2 can read layer 1's output
via `PreviousLayerResultJson`, enabling multi-stage pipelines.

```
create_session(sourceCode)
    └─► run_layer(sessionId, parallelism=N)          ← layer 1
            └─► run_layer(sessionId, parallelism=N,   ← layer 2, reads layer 1 results
                          previousLayerResultJson=...) 
```

If a layer fails, call `create_session` again with fixed code and re-run from the last successful
layer's `resultJson` — no earlier work is lost.

---

## The IAgentComputation Interface

Every session requires a C# class implementing this interface from `Parcs.Agent.Runtime`:

```csharp
public interface IAgentComputation
{
    Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);
}
```

### AgentLayerInput fields available to each worker

| Field | Type | Description |
|---|---|---|
| `WorkerIndex` | `int` | 0-based index within this layer's worker pool |
| `TotalWorkers` | `int` | Total workers in this layer |
| `PreviousLayerResultJson` | `string?` | JSON output from the previous `run_layer` call (null for first layer) |
| `CustomData` | `string?` | Shared string payload broadcast to all workers |
| `Parameters` | `Dictionary<string,string>` | Named key/value pairs passed at submission time |

### Returning results

```csharp
return AgentLayerResult.Ok(outputJson);    // success — outputJson is any JSON string
return AgentLayerResult.Error("message");  // failure — layer status becomes Failed
```

### Required usings (always included automatically in body-only mode)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Parcs.Agent.Runtime;
```

If you submit a **full class** (containing `class` + `IAgentComputation`), include your own usings.
If you submit just a **method body**, the wrapper class and usings are added automatically.

---

## MCP Tools

### `get_cluster_info`
Returns current cluster capacity. Call this first to decide parallelism.

```json
{
  "workerNodeCount": 3,
  "maxParallelism": 21,
  "daemonCpuRequestMillicores": 500
}
```

Never request more workers than `maxParallelism`. KEDA autoscales nodes on demand, so using the
full `maxParallelism` is safe and encouraged for large jobs.

---

### `create_session`

```
create_session(sourceCode: string) → { sessionId, createdAt, message } | { error }
```

- Compiles the C# source with Roslyn
- Returns `sessionId` on success; returns `{ error }` with compiler diagnostics on failure
- On compile error: fix the code and call `create_session` again — no state is lost

---

### `run_layer` *(preferred — blocks until done)*

```
run_layer(
  sessionId:               string,
  parallelism:             int,
  previousLayerResultJson: string? = null,
  customData:              string? = null,
  parametersJson:          string? = null   // JSON object e.g. '{"start":"0","end":"1000"}'
) → { layerId, sessionId, status, submittedAt, completedAt, resultJson?, errorMessage? }
```

`status` is `"Completed"` or `"Failed"`.

`resultJson` is a `LayerOutputDto`:
```json
{
  "sessionId": "...",
  "layerId": "...",
  "totalElapsedSeconds": 4.2,
  "results": [
    { "workerIndex": 0, "success": true,  "outputData": "...", "elapsedSeconds": 4.1 },
    { "workerIndex": 1, "success": false, "errorMessage": "...", "elapsedSeconds": 0.3 }
  ]
}
```

---

### `submit_layer` + `get_layer_results` *(async — fire and poll)*

Use when you want to dispatch a long layer without blocking. Poll `get_layer_results` every 2–5
seconds until `status` is `"Completed"` or `"Failed"`. Prefer `run_layer` for most scenarios.

---

### `list_sessions`

Lists all active sessions in this MCP server instance. Useful for resuming a pipeline after a
connection drop.

---

## Patterns

### Work partitioning by index

The standard approach: each worker handles its slice of the total work.

```csharp
// Divide range [0, total) evenly across workers
int total = int.Parse(input.Parameters["total"]);
int chunkSize = (total + input.TotalWorkers - 1) / input.TotalWorkers;
int start = input.WorkerIndex * chunkSize;
int end   = Math.Min(start + chunkSize, total);

for (int i = start; i < end; i++)
{
    // process item i
}
```

### Reading previous layer results

```csharp
var previousResults = JsonSerializer.Deserialize<List<MyResultType>>(
    input.PreviousLayerResultJson!);
```

Each worker in the previous layer wrote its own JSON. The `results` array in `LayerOutputDto`
contains each worker's `outputData` — deserialize appropriately in the coordinator layer.

### Seeded randomness (reproducible across workers)

```csharp
var rng = new Random(input.WorkerIndex * 1337 + 42);
```

Using a seed derived from `WorkerIndex` ensures deterministic and non-overlapping random streams.

---

## Multi-layer pipeline example

```
Layer 1 (N workers): Each worker scans its partition → returns partial results JSON
Layer 2 (1 worker):  Aggregator reads previousLayerResultJson → merges → returns final answer
```

Pass `parallelism=1` on the aggregator layer and use `previousLayerResultJson` to feed it all
of layer 1's output.

---

## Error handling

| Situation | What to do |
|---|---|
| `create_session` returns `{ error }` | Read the `[CSXXXX]` diagnostic, fix the code, re-call `create_session` |
| `run_layer` returns `status: "Failed"` | Check `errorMessage`; fix code with `create_session`; re-run passing last successful `resultJson` as `previousLayerResultJson` |
| Individual worker `success: false` | Decide whether to retry the whole layer or handle partial results |
| Session not found | Call `list_sessions` to find active sessions; create a new one if needed |

---

## Tips

- Always call `get_cluster_info` first and cap `parallelism` at `maxParallelism`.
- Prefer `run_layer` over `submit_layer` + polling unless the layer will run for many minutes.
- Serialize worker outputs as compact JSON — `outputData` is a string field on the result.
- Avoid `dynamic` keyword if possible; if you need it, it's supported (Microsoft.CSharp is referenced).
- For CPU-bound work, each daemon gets 500m CPU — prefer work units of at least a few seconds per worker to amortise scheduling overhead.
- The cluster autoscales via KEDA; spinning up new nodes takes ~60–90 seconds on first use. Subsequent layers reuse warm pods.
