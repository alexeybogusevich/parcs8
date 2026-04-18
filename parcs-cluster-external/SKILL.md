---
name: parcs-cluster
description: >
  Use this skill whenever you need to run parallel or distributed computation. Triggers include:
  any task that would benefit from parallelism, number crunching, brute-force search, simulation,
  data processing at scale, matrix operations, statistical sampling, prime finding, sorting large
  datasets, Monte Carlo methods, or any algorithm that can be split into independent chunks of
  work. Also use when the user mentions PARCS, the cluster, distributed workers, or parallel C#
  execution. When in doubt — if the task involves looping over a large range or dataset and the
  iterations are independent — this skill applies.
---

# PARCS — Parallel Distributed Compute for AI Agents

## What is PARCS?

PARCS is a distributed computing platform that lets you run C# code across multiple parallel
workers in a cloud cluster. You write a C# class, submit it to the cluster, specify how many
workers to use, and the cluster fans the work out, runs it in parallel, and returns all the
results to you. Think of it as: "run this function on 20 machines simultaneously."

This is useful whenever a problem can be split into independent parts — for example:
- Searching a large number range (each worker scans a different slice)
- Running many simulations (each worker runs its own batch)
- Processing large datasets (each worker handles a partition)
- Any loop where iterations don't depend on each other

---

## Step 0 — Connect to the cluster

The cluster exposes an MCP server over SSE. Add it to Claude Code once:

```bash
claude mcp add parcs --transport sse http://34.76.43.4:8080/sse
```

After this, the PARCS tools (`get_cluster_info`, `create_session`, `run_layer`, etc.) are
available in your session.

---

## Step 1 — Check cluster capacity

Always call `get_cluster_info` first. It tells you how many workers are available.

**Tool:** `get_cluster_info`  
**Parameters:** none  
**Returns:**
```json
{
  "workerNodeCount": 3,
  "maxParallelism": 21,
  "daemonCpuRequestMillicores": 500
}
```

- `maxParallelism` — the maximum number of workers you can use at once. Never exceed this.
- `daemonCpuRequestMillicores` — each worker gets 500m of CPU (half a core).
- The cluster autoscales: if nodes need to spin up, the first run may take 60–90 seconds longer.

---

## Step 2 — Write your C# computation

Your code must be a C# class that implements the `IAgentComputation` interface:

```csharp
public interface IAgentComputation
{
    Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);
}
```

Each worker receives an `AgentLayerInput` describing its slice of the work, does its computation,
and returns an `AgentLayerResult`.

### AgentLayerInput — what each worker knows about itself

| Property | Type | What it contains |
|---|---|---|
| `WorkerIndex` | `int` | This worker's position in the pool, 0-based. Worker 0 is first, worker N-1 is last. |
| `TotalWorkers` | `int` | How many workers are running in this layer in total. |
| `PreviousLayerResultJson` | `string?` | JSON output from the previous layer (null on the first layer). Used to chain layers together. |
| `CustomData` | `string?` | An arbitrary string you passed when calling `run_layer`. Every worker gets the same value. |
| `Parameters` | `Dictionary<string,string>` | Named string parameters you passed when calling `run_layer`. |

### AgentLayerResult — how to return results

```csharp
// Success: pass any JSON string as the output
return AgentLayerResult.Ok(JsonSerializer.Serialize(new { answer = 42 }));

// Failure: report an error message
return AgentLayerResult.Error("Something went wrong: ...");
```

### Full example — find primes in a range

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Parcs.Agent.Runtime;

public sealed class PrimeFinder : IAgentComputation
{
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct)
    {
        // Each worker scans its own slice of the range
        int total = int.Parse(input.Parameters["total"]);
        int chunkSize = (total + input.TotalWorkers - 1) / input.TotalWorkers;
        int start = Math.Max(2, input.WorkerIndex * chunkSize);
        int end   = Math.Min(start + chunkSize, total);

        var primes = new List<int>();
        for (int n = start; n < end; n++)
        {
            if (IsPrime(n)) primes.Add(n);
        }

        var json = JsonSerializer.Serialize(new
        {
            workerIndex = input.WorkerIndex,
            start,
            end,
            count = primes.Count,
            primes,
        });

        return Task.FromResult(AgentLayerResult.Ok(json));
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }
}
```

### Shortcut — body-only mode

If you don't want to write the full class, you can submit just the body of `ExecuteAsync` and it
will be wrapped automatically. The following usings are always available in body-only mode:

```
System, System.Collections.Generic, System.Linq, System.Text,
System.Text.Json, System.Threading, System.Threading.Tasks, Parcs.Agent.Runtime
```

Body-only example (equivalent to the full class above but shorter):

```csharp
int total = int.Parse(input.Parameters["total"]);
int chunkSize = (total + input.TotalWorkers - 1) / input.TotalWorkers;
int start = Math.Max(2, input.WorkerIndex * chunkSize);
int end   = Math.Min(start + chunkSize, total);

var primes = new List<int>();
for (int n = start; n < end; n++)
{
    bool isPrime = n >= 2 && Enumerable.Range(2, (int)Math.Sqrt(n) - 1)
                                       .All(i => n % i != 0);
    if (isPrime) primes.Add(n);
}

return AgentLayerResult.Ok(JsonSerializer.Serialize(new {
    workerIndex = input.WorkerIndex,
    count = primes.Count,
    primes,
}));
```

---

## Step 3 — Compile and register your code

**Tool:** `create_session`  
**Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `sourceCode` | `string` | Full C# class implementing `IAgentComputation`, or just the `ExecuteAsync` method body. |

**Returns on success:**
```json
{
  "sessionId": "abc123",
  "createdAt": "2025-11-12T10:00:00Z",
  "message": "Compiled successfully. Use sessionId with run_layer."
}
```

**Returns on compile failure:**
```json
{
  "error": "Compilation failed:\n  [CS0103] The name 'Foo' does not exist in the current context (line 5)"
}
```

If compilation fails, read the `[CSXXXX]` error code and line number, fix the code, and call
`create_session` again. No state is lost — you can call it as many times as needed.

---

## Step 4 — Run the computation

**Tool:** `run_layer`  
**Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `sessionId` | `string` | ✓ | The session ID returned by `create_session`. |
| `parallelism` | `int` | ✓ | Number of parallel workers. Must be between 1 and `maxParallelism`. |
| `previousLayerResultJson` | `string?` | — | Pass the `resultJson` from a previous `run_layer` here to give workers access to earlier results. Null for the first layer. |
| `customData` | `string?` | — | Any string broadcast to all workers via `input.CustomData`. |
| `parametersJson` | `string?` | — | JSON object of named parameters, e.g. `'{"total":"100000"}'`. Workers read these via `input.Parameters["total"]`. |

**Returns:**
```json
{
  "layerId": "layer-xyz",
  "sessionId": "abc123",
  "status": "Completed",
  "submittedAt": "2025-11-12T10:00:01Z",
  "completedAt": "2025-11-12T10:00:06Z",
  "resultJson": "{ ... }",
  "errorMessage": null
}
```

`status` is either `"Completed"` or `"Failed"`.

When `"Completed"`, `resultJson` contains a `LayerOutputDto`:

```json
{
  "sessionId": "abc123",
  "layerId": "layer-xyz",
  "totalElapsedSeconds": 5.2,
  "results": [
    {
      "workerIndex": 0,
      "success": true,
      "outputData": "{\"count\": 1229, \"primes\": [...]}",
      "elapsedSeconds": 5.1
    },
    {
      "workerIndex": 1,
      "success": true,
      "outputData": "{\"count\": 1033, \"primes\": [...]}",
      "elapsedSeconds": 4.9
    }
  ]
}
```

Each entry in `results` corresponds to one worker. `outputData` is the string you passed to
`AgentLayerResult.Ok(...)`. Deserialize it as needed to aggregate the results.

---

## Complete worked example — Monte Carlo π estimation

```
Step 1: get_cluster_info → maxParallelism = 21, use 20 workers

Step 2: create_session with this code:
```

```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Parcs.Agent.Runtime;

public sealed class MonteCarloPI : IAgentComputation
{
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct)
    {
        int samples = 5_000_000;
        long hits = 0;
        // Seed per worker so each worker gets a different random stream
        var rng = new Random(input.WorkerIndex * 1337 + 42);

        for (int i = 0; i < samples; i++)
        {
            double x = rng.NextDouble();
            double y = rng.NextDouble();
            if (x * x + y * y <= 1.0) hits++;
        }

        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new
        {
            workerIndex = input.WorkerIndex,
            hits,
            samples,
        })));
    }
}
```

```
Step 3: run_layer(sessionId, parallelism=20)

Step 4: aggregate results
  totalHits    = sum of hits across all workers
  totalSamples = sum of samples across all workers
  π ≈ 4 * totalHits / totalSamples

With 20 workers × 5M samples = 100M total samples → π ≈ 3.1416 (accurate to ~4 decimal places)
```

---

## Multi-layer pipelines

Layers can be chained: the output of layer 1 becomes the input of layer 2.

```
Layer 1 (20 workers): Each worker scans its partition → returns partial summary JSON
Layer 2 (1 worker):   Single aggregator reads all partial summaries → merges → final answer
```

Pass `previousLayerResultJson` when calling `run_layer` for subsequent layers:

```python
# After layer 1:
layer1_result = run_layer(sessionId, parallelism=20, parametersJson='{"total":"1000000"}')

# Layer 2 reads layer 1's output:
layer2_result = run_layer(
    sessionId,
    parallelism=1,
    previousLayerResultJson=layer1_result["resultJson"]  # ← pass the full resultJson here
)
```

In layer 2's code, deserialize `input.PreviousLayerResultJson` to access the layer 1 results:

```csharp
var layer1Output = JsonSerializer.Deserialize<LayerOutputDto>(input.PreviousLayerResultJson!);
foreach (var workerResult in layer1Output.Results)
{
    var partial = JsonSerializer.Deserialize<MyPartialResult>(workerResult.OutputData);
    // ... aggregate
}
```

---

## Other useful tools

### `list_sessions`
Lists all active sessions on the server. Useful for resuming work after a connection drop.

```json
{
  "count": 2,
  "sessions": [
    { "sessionId": "abc123", "createdAt": "..." },
    { "sessionId": "def456", "createdAt": "..." }
  ]
}
```

### `submit_layer` + `get_layer_results` (async variant)
If you want to fire off a layer without waiting for it to complete, use `submit_layer` to get a
`layerId` immediately, then poll `get_layer_results` every few seconds until `status` is terminal.
For most tasks, prefer `run_layer` which handles waiting automatically.

---

## Error handling

| Problem | What to do |
|---|---|
| `create_session` returns `{ error: "Compilation failed..." }` | Read the `[CSXXXX]` error and line number. Fix the code and call `create_session` again. |
| `run_layer` returns `status: "Failed"` | Read `errorMessage`. Fix code with a new `create_session` call, then re-run from the last successful layer. |
| One worker has `success: false` | The other workers' results are still usable. Decide whether to retry the whole layer or work with partial results. |
| First run is slow (60–90 seconds) | Normal — the cluster is scaling up nodes. Subsequent layers run on warm pods and are much faster. |
| `"Session not found"` error | The session may have expired. Call `list_sessions` to check, or `create_session` to start fresh. |

---

## Common patterns

### Divide a range evenly across workers
```csharp
int total = int.Parse(input.Parameters["total"]);
int chunkSize = (total + input.TotalWorkers - 1) / input.TotalWorkers;
int start = input.WorkerIndex * chunkSize;
int end   = Math.Min(start + chunkSize, total);
// process items [start, end)
```

### Reproducible randomness per worker
```csharp
var rng = new Random(input.WorkerIndex * 1337 + 42);
// Each worker gets a different, deterministic random stream
```

### Passing configuration to all workers
```
parametersJson: '{"maxN":"1000000","threshold":"0.05"}'
```
```csharp
int maxN      = int.Parse(input.Parameters["maxN"]);
double thresh = double.Parse(input.Parameters["threshold"]);
```

### Serialising complex output
```csharp
var output = new
{
    workerIndex  = input.WorkerIndex,
    partialSum   = sum,
    itemsChecked = count,
    found        = results,
};
return AgentLayerResult.Ok(JsonSerializer.Serialize(output));
```

---

## Tips

- Keep per-worker work units at least a few seconds long — very short tasks waste scheduling time.
- `dynamic` keyword is supported (the Microsoft.CSharp binder is available).
- Use `JsonSerializer` from `System.Text.Json` — it's always available.
- You don't need to import NuGet packages; only BCL + `Parcs.Agent.Runtime` are available.
- Workers run in isolated pods with no shared memory — all coordination goes through `resultJson`.
- Parallelism is free up to `maxParallelism`; always use as many workers as the problem allows.
