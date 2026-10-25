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

The PARCS cluster runs C# code in parallel across many daemon workers. You submit C# source, PARCS
compiles it with Roslyn, runs it on N workers, and returns the results. You write only the
sequential code of one worker; distribution, scheduling and result collection are the platform's job.

## Execution Model

A computation is a sequence of **layers**. A layer runs one `IAgentComputation` class of a session on
N workers in parallel. A later layer reads earlier layers' results through `previousLayerIds`: the
server passes them to every worker, so you never copy result JSON yourself.

```
create_session(sourceCode)                         → sessionId, entryPoints
run_layer(sessionId, parallelism=N, entryPoint="Map", layerKey="map")              → layerId A
run_layer(sessionId, parallelism=1, entryPoint="Reduce", previousLayerIds=[A], layerKey="reduce")
```

**No call blocks for long.** `run_layer` waits at most `waitSeconds` (default 60, max 200). If the
layer is still running, it returns `status: "Running"` with the `layerId` — call `wait_layer(layerId)`
until it is done. Starting workers can take minutes when the cluster has to add nodes.

**Every call is safe to repeat.** Give each layer a `layerKey` (e.g. `"map-3"`). Repeating `run_layer`
with the same key and arguments returns the same layer (`reused: true`) instead of running it again,
so after a timeout or a dropped connection just repeat the call. The same source code passed to
`create_session` returns the same session. `list_layers(sessionId)` lists everything already run.

## Result shape

Every tool returns one envelope:

```json
{ "ok": true, "status": "Completed", "sessionId": "…", "layerId": "…", "layerKey": "reduce",
  "layer": { "parallelism": 1, "successCount": 1, "failureCount": 0, "queuedSeconds": 0.0, "elapsedSeconds": 3.2 },
  "output": { "var99": 0.0213 } }
```

- `ok` is false exactly when `error` is set: `{ code, message, retryable, failedWorkers?, diagnostics? }`.
- Layer `status`: `Queued` / `Running` (ok — wait), `Completed`, `CompletedWithFailures` (some workers
  failed; `error.failedWorkers` lists them), `Failed`, `Cancelled`.
- With `parallelism: 1`, `output` is that worker's output, already parsed when it is JSON.
- `view: "outputs"` adds `outputs[]` (one per worker, `null` for failed ones); `view: "full"` adds
  `workers[]` with errors and timings. The default summary keeps results small — make a final
  `parallelism: 1` layer produce the answer instead of reading every worker's output.
- Error codes: `invalid_argument`, `session_not_found`, `layer_not_found`, `layer_key_conflict`,
  `previous_layer_not_ready`, `previous_layer_failed`, `compile_error`, `capacity_exceeded`,
  `not_ready`, `worker_error`, `out_of_memory`, `daemon_timeout`, `no_daemons`, `host_unavailable`,
  `job_failed`, `cancelled`. Repeat a call unchanged only when `retryable` is true.

---

## The IAgentComputation Interface

A session holds one or more classes implementing this interface from `Parcs.Agent.Runtime`:

```csharp
public interface IAgentComputation
{
    Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);
}
```

Put the map and the reduce class in **one** session and select one per layer with `entryPoint`
(full or simple class name; `create_session` returns the list as `output.entryPoints`).

### AgentLayerInput fields available to each worker

| Field | Type | Description |
|---|---|---|
| `WorkerIndex` | `int` | 0-based index within this layer's workers |
| `TotalWorkers` | `int` | Number of workers in this layer |
| `PreviousLayerResults` | `IReadOnlyList<string>` | Result document of each layer in `previousLayerIds`, in that order |
| `PreviousLayerResultJson` | `string?` | The first of them (null for a first layer) |
| `GetPreviousOutputs(parentIndex = 0)` | `IReadOnlyList<string?>` | The parent's per-worker outputs in worker order, `null` for failed workers |
| `CustomData` | `string?` | Payload sent unchanged to every worker |
| `WorkerInput` | `string?` | This worker's own entry of `workerInputs` |
| `Parameters` | `Dictionary<string,string>` | Named values sent unchanged to every worker |
| `InheritedOutputs` | `IReadOnlyList<string?>` | For each parent, the output of its worker with this `WorkerIndex` (the only parent data with `inherit: "byIndex"`) |
| `Topology` | `AgentTopology` | Channels to this worker's neighbours when the layer has a `topology` (see below) |

Workers have no network or file-system access besides their topology channels: generate data from
seeds or pass small inputs in `customData`, `parameters` or `workerInputs`.

### Returning results

```csharp
return AgentLayerResult.Ok(outputJson);    // success — outputJson is any JSON string
return AgentLayerResult.Error("message");  // failure — this worker counts as failed
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
Compile errors come back as `error.diagnostics` with line numbers of the code you sent.

---

## Calling the tools from code (when you have the `eval` tool)

If an `eval` JavaScript tool is available, drive the whole pipeline from one script instead of one
tool call per step. Each PARCS tool is `tools.<camelCaseName>(args)` and resolves to the envelope as a
plain object — check `r.ok` and `r.status`; errors do not throw.

```javascript
const WAIT = 25;
async function layer(args) {
  let r = await tools.runLayer({ waitSeconds: WAIT, ...args });
  while (r.ok && (r.status === "Running" || r.status === "Queued"))
    r = await tools.waitLayer({ layerId: r.layerId, waitSeconds: WAIT });
  if (!r.ok) throw new Error(`${r.error.code}: ${r.error.message}`);
  return r;
}
const info = (await tools.getClusterInfo({})).output;
const s = await tools.createSession({ sourceCode: SOURCE });
if (!s.ok) throw new Error(JSON.stringify(s.error.diagnostics));
const p = Math.min(16, info.maxParallelism);
const map = await layer({ sessionId: s.sessionId, entryPoint: "Map", parallelism: p, layerKey: "map" });
const red = await layer({ sessionId: s.sessionId, entryPoint: "Reduce", parallelism: 1,
                          previousLayerIds: [map.layerId], layerKey: "reduce" });
red.output;   // the last expression is all you see
```

Keep layer keys stable: if a script stops part-way, run it again and the finished layers are
returned instead of re-run.

---

<!-- BEGIN GENERATED TOOL REFERENCE -->
<!-- Generated by generate_skill.py from the server's tools/list. Do not edit by hand. -->

## MCP Tools

### `get_cluster_info()`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Returns cluster capacity in output = { scalingMode: 'elastic'|'static', workerNodeCount, currentCapacity, maxParallelism, daemonCpuRequestMillicores, capacitySource: 'kubernetes'|'dns'|'configuration'|'fallback', freeWorkers, runningLayers, queuedLayers }. Call once at the start to size parallelism. Never request more than maxParallelism. In elastic mode, workers above currentCapacity need new nodes (minutes of start-up); in static mode a fixed pool serves all layers and nothing is provisioned. Layers that do not fit in freeWorkers wait in a server-side queue. capacitySource 'fallback' means the numbers are a configured guess.

### `create_session(sourceCode)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Compiles C# source with one or more classes implementing IAgentComputation from Parcs.Agent.Runtime:
  public interface IAgentComputation { Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct); }
AgentLayerInput: WorkerIndex, TotalWorkers, SessionId, LayerId, CustomData (same for every worker), WorkerInput (workerInputs[WorkerIndex] of run_layer), Parameters (Dictionary<string,string>), PreviousLayerResults (one JSON document per parent layer), PreviousLayerResultJson (= the first of them) and GetPreviousOutputs(parentIndex = 0), which returns the parent's per-worker outputs in worker order (null for failed workers), InheritedOutputs (for each parent, the output of its worker with this WorkerIndex), and Topology (see run_layer's topology): Topology.Neighbors, Topology[peer] -> IAgentChannel { SendAsync(string), ReceiveAsync() } plus the extensions SendJsonAsync<T>, ReceiveJsonAsync<T>, ExchangeJsonAsync<T>, and Left/Right (ring, line), Parent/Children (tree, star), Dimensions/Partner(d) (hypercube), Row/Column/North/South/West/East (grid, torus). Sends never block; a receive throws TimeoutException after channelTimeoutSeconds and IOException when the neighbour failed. Return AgentLayerResult.Ok(outputJson) or AgentLayerResult.Error(message). Workers have no network or file access besides their topology channels.
Returns status 'Created' or 'Reused' (the same source returns the same session), sessionId and output = { entryPoints: [full class names] }; pass one as run_layer's entryPoint when there are several. On compile failure: error.code 'compile_error' with diagnostics [{ id, line, column, message }].

| Argument | Type | Required | Description |
|---|---|---|---|
| `sourceCode` | `string` | yes | Complete C# source with one or more IAgentComputation classes, or just the body of ExecuteAsync (usings and a class wrapper are then added). |

### `run_layer(sessionId, parallelism, waitSeconds?, layerKey?, entryPoint?, previousLayerIds?, customData?, parameters?, workerInputs?, failFast?, topology?, topologyEdges?, channelTimeoutSeconds?, inherit?, view?)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Runs a session's computation on 'parallelism' workers and waits up to waitSeconds (default 60, max 200) for it to finish. If it is still running, returns status 'Running' with layerId: call wait_layer(layerId). Pass a layerKey to make the call idempotent: repeating it with the same key and arguments returns the same layer instead of starting another job, with reused: true (a key whose layer Failed or was Cancelled starts a new attempt). Chain layers with previousLayerIds; workers read the parents' results through input.PreviousLayerResults, or only their own share with inherit 'byIndex'. Give the workers direct channels to each other with topology: they all run at once and exchange data peer to peer (halo exchange on a grid, token passing on a ring, reductions up a tree, all-reduce over hypercube dimensions) instead of a layer per exchange. Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. layer = { entryPoint, parallelism, previousLayerIds, topology?, inherit?, successCount, failureCount, queuedSeconds, elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = { workerIndex, success, output, error, elapsedSeconds }. Error codes: invalid_argument, session_not_found, layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.

| Argument | Type | Required | Description |
|---|---|---|---|
| `sessionId` | `string` | yes | Session ID from create_session. |
| `parallelism` | `integer` | yes | Number of workers; at most maxParallelism from get_cluster_info. |
| `waitSeconds` | `integer \| null` | no | Seconds to wait for completion before returning status 'Running' (0–200, default 60). |
| `layerKey` | `string \| null` | no | Caller-chosen idempotency key, unique per logical layer within the session, e.g. 'map-3'. |
| `entryPoint` | `string \| null` | no | Full or simple name of the IAgentComputation class to run; required when the session has several. |
| `previousLayerIds` | `array \| null` | no | layerIds of completed parent layers, in the order workers see them in input.PreviousLayerResults. |
| `customData` | `string \| null` | no | Small string sent unchanged to every worker as input.CustomData. |
| `parameters` | `object \| null` | no | Named parameters sent unchanged to every worker as input.Parameters. |
| `workerInputs` | `array \| null` | no | One string per worker (exactly 'parallelism' entries); worker i reads workerInputs[i] as input.WorkerInput. |
| `failFast` | `boolean \| null` | no | If true, the first failed worker fails the whole layer (status Failed) instead of CompletedWithFailures. Default: true with a topology, false otherwise. |
| `topology` | `string \| null` | no | Direct worker-to-worker channels, input.Topology: 'none' (default), 'ring', 'line' (a ring without the wrap), 'star' (worker 0 is the hub), 'tree' or 'tree:K' (K-ary, worker 0 is the root, parent (i-1)/K), 'hypercube' (parallelism a power of two, partners i XOR 2^d), 'grid' or 'grid:RxC' (4-neighbour, R*C = parallelism; the squarest shape when RxC is omitted), 'torus' or 'torus:RxC' (grid with wrap-around), 'mesh' (all pairs) or 'custom' (with topologyEdges). |
| `topologyEdges` | `array \| null` | no | Edges of topology 'custom': [[i, j], ...] worker pairs, each joined by one two-way channel. |
| `channelTimeoutSeconds` | `integer \| null` | no | Seconds a topology channel receive waits before throwing TimeoutException (1–900, default 60); also bounds wiring the channels. |
| `inherit` | `string \| null` | no | 'all' (default): every worker gets every parent's whole result. 'byIndex': worker i gets only output i of each parent, in input.InheritedOutputs (parents must have the same parallelism). |
| `view` | `string \| null` | no | 'summary' (default), 'outputs' or 'full'. |

### `submit_layer(sessionId, parallelism, layerKey?, entryPoint?, previousLayerIds?, customData?, parameters?, workerInputs?, failFast?, topology?, topologyEdges?, channelTimeoutSeconds?, inherit?)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Same as run_layer with waitSeconds 0: starts the layer and returns at once with status 'Queued' or 'Running' and layerId (or the finished layer when layerKey names one). Use wait_layer to wait for it. Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. layer = { entryPoint, parallelism, previousLayerIds, topology?, inherit?, successCount, failureCount, queuedSeconds, elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = { workerIndex, success, output, error, elapsedSeconds }. Error codes: invalid_argument, session_not_found, layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.

| Argument | Type | Required | Description |
|---|---|---|---|
| `sessionId` | `string` | yes | Session ID from create_session. |
| `parallelism` | `integer` | yes | Number of workers; at most maxParallelism from get_cluster_info. |
| `layerKey` | `string \| null` | no | Caller-chosen idempotency key, see run_layer. |
| `entryPoint` | `string \| null` | no | IAgentComputation class to run, see run_layer. |
| `previousLayerIds` | `array \| null` | no | layerIds of completed parent layers. |
| `customData` | `string \| null` | no | Small string sent to every worker as input.CustomData. |
| `parameters` | `object \| null` | no | Named parameters sent to every worker as input.Parameters. |
| `workerInputs` | `array \| null` | no | One string per worker, read as input.WorkerInput. |
| `failFast` | `boolean \| null` | no | Fail the whole layer on the first failed worker; default true with a topology. |
| `topology` | `string \| null` | no | Worker-to-worker channels, see run_layer. |
| `topologyEdges` | `array \| null` | no | Edges of topology 'custom', see run_layer. |
| `channelTimeoutSeconds` | `integer \| null` | no | Receive timeout of topology channels, see run_layer. |
| `inherit` | `string \| null` | no | 'all' (default) or 'byIndex', see run_layer. |

### `wait_layer(layerId, waitSeconds?, view?)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Waits up to waitSeconds (default 60, max 200) for a layer to finish and returns it; returns at once if it already has. Status 'Running' or 'Queued' means call wait_layer again. Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. layer = { entryPoint, parallelism, previousLayerIds, topology?, inherit?, successCount, failureCount, queuedSeconds, elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = { workerIndex, success, output, error, elapsedSeconds }. Error codes: invalid_argument, session_not_found, layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.

| Argument | Type | Required | Description |
|---|---|---|---|
| `layerId` | `string` | yes | layerId from run_layer, submit_layer or list_layers. |
| `waitSeconds` | `integer \| null` | no | Seconds to wait (0–200, default 60). |
| `view` | `string \| null` | no | 'summary' (default), 'outputs' or 'full'. |

### `get_layer_result(layerId, view?)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Returns a layer's current state without waiting. Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. layer = { entryPoint, parallelism, previousLayerIds, topology?, inherit?, successCount, failureCount, queuedSeconds, elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = { workerIndex, success, output, error, elapsedSeconds }. Error codes: invalid_argument, session_not_found, layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.

| Argument | Type | Required | Description |
|---|---|---|---|
| `layerId` | `string` | yes | layerId from run_layer, submit_layer or list_layers. |
| `view` | `string \| null` | no | 'summary' (default), 'outputs' or 'full'. |

### `list_layers(sessionId)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Lists a session's layers in submission order in output = { layers: [{ layerId, layerKey, status, entryPoint, parallelism, previousLayerIds, submittedAt, completedAt }] }. Recovers layerIds lost to a timeout or a dropped connection.

| Argument | Type | Required | Description |
|---|---|---|---|
| `sessionId` | `string` | yes | Session ID from create_session. |

### `list_sessions()`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Lists sessions newest first in output = { sessions: [{ sessionId, createdAt, entryPoints, layers: [{ layerId, layerKey, status, submittedAt }] }] }. Useful for resuming after a connection interruption.

### `cancel_layer(layerId)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Cancels a Queued or Running layer and frees its workers; returns the layer (status Cancelled once the cancellation lands). A finished layer is returned unchanged. Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. layer = { entryPoint, parallelism, previousLayerIds, topology?, inherit?, successCount, failureCount, queuedSeconds, elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = { workerIndex, success, output, error, elapsedSeconds }. Error codes: invalid_argument, session_not_found, layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.

| Argument | Type | Required | Description |
|---|---|---|---|
| `layerId` | `string` | yes | layerId to cancel. |

### `delete_session(sessionId)`

Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. Deletes a session and all its layers, cancelling the ones still queued or running. Sessions and finished layers are otherwise kept, also across server restarts. Returns status 'Deleted' with output = { deletedLayers }, or error.code 'session_not_found'.

| Argument | Type | Required | Description |
|---|---|---|---|
| `sessionId` | `string` | yes | Session ID from create_session or list_sessions. |

<!-- END GENERATED TOOL REFERENCE -->

---

## Patterns

### Work partitioning by index

```csharp
// Divide range [0, total) evenly across workers
int total = int.Parse(input.Parameters["total"]);
int chunkSize = (total + input.TotalWorkers - 1) / input.TotalWorkers;
int start = input.WorkerIndex * chunkSize;
int end   = Math.Min(start + chunkSize, total);
```

### Aggregating the previous layer

```csharp
var partials = input.GetPreviousOutputs()
    .Select(o => o is null ? throw new InvalidOperationException("a map worker failed")
                           : JsonSerializer.Deserialize<Partial>(o)!)
    .ToList();
```

### Seeded randomness (reproducible across workers)

```csharp
var rng = new Random(input.WorkerIndex * 1337 + 42);
```

### Workers that talk to each other: topologies

A layer started with `topology` gives its workers direct channels to their neighbours. All workers
run at once, so an algorithm that would need one layer per exchange (and a round trip through the
server each time) runs as a single layer:

| `topology` | Neighbours of worker i | Helpers on `input.Topology` |
|---|---|---|
| `ring` | i-1 and i+1, wrapping | `Left`, `Right` |
| `line` | i-1 and i+1, no wrap | `Left`, `Right` (null at the ends) |
| `star` | worker 0 ↔ every other worker | `Parent` (0), `Children` (of worker 0) |
| `tree`, `tree:K` | parent (i-1)/K, children K·i+1 … K·i+K | `Parent` (null at the root, worker 0), `Children` |
| `hypercube` | i XOR 2^d for each dimension d (parallelism a power of 2) | `Dimensions`, `Partner(d)` |
| `grid`, `grid:RxC` | 4 neighbours, R·C = parallelism (squarest shape if omitted) | `Row`, `Column`, `North`, `South`, `West`, `East` |
| `torus`, `torus:RxC` | a grid that wraps around | as grid |
| `mesh` | every other worker | — |
| `custom` + `topologyEdges: [[i, j], ...]` | the listed pairs | — |

`input.Topology.Neighbors` lists the neighbour indices; `input.Topology[peer]` is the channel to one:

```csharp
var t = input.Topology;
await t[t.Right!.Value].SendJsonAsync(value, ct);         // never blocks
var fromLeft = await t[t.Left!.Value].ReceiveJsonAsync<long>(ct);
// hypercube all-reduce: v holds the global sum on every worker afterwards
for (var d = 0; d < t.Dimensions; d++) v += await t[t.Partner(d)].ExchangeJsonAsync(v, ct);
```

- Sends are buffered, so neighbours may both send before they receive (halo exchange) without
  deadlocking. Messages on one channel arrive in order.
- A receive waits at most `channelTimeoutSeconds` (default 60) and then throws `TimeoutException`
  ("channel_timeout"); if the neighbour failed, it throws `IOException` at once. Every receive needs
  a matching send on the other side.
- `failFast` defaults to true for topology layers: one failed worker stalls its neighbours anyway.
- `parallelism` must fit in `freeWorkers`: all workers of the layer run at the same time.

### Passing each worker only its share: `inherit: "byIndex"`

With `inherit: "byIndex"`, worker i of the child layer receives only output i of each parent in
`input.InheritedOutputs` (parents need the same parallelism), instead of every parent's whole result.
Use it for map → map pipelines; keep the default `"all"` for reductions.

---

## Error handling

| Situation | What to do |
|---|---|
| `compile_error` | Read `error.diagnostics`, fix the code, call `create_session` again |
| `status: "Running"` / `"Queued"` | Call `wait_layer(layerId)`; nothing is lost |
| A call timed out or the connection dropped | Repeat it with the same `layerKey`, or find the layer with `list_layers` |
| `CompletedWithFailures` | `error.failedWorkers` lists them; the next layer still sees the others' results. Pass `failFast: true` to fail the whole layer instead |
| `Failed` with `retryable: true` (e.g. `daemon_timeout`) | Repeat the call; the same `layerKey` starts a new attempt |
| `worker_error` / `Failed` in your code | Fix the class, `create_session`, re-run from the last good `layerId` |
| `capacity_exceeded` | Lower `parallelism` to at most `maxParallelism` |
| A layer you no longer need is running | `cancel_layer(layerId)` frees its workers |

---

## Tips

- Call `get_cluster_info` first and keep `parallelism` ≤ `maxParallelism`. In `elastic` mode, workers
  beyond `currentCapacity` need new nodes (minutes); in `static` mode a fixed pool serves all layers
  and layers that do not fit wait in a queue.
- Give every layer a `layerKey`, and make work units at least a few seconds per worker so start-up
  overhead does not dominate.
- Serialize worker outputs as compact JSON with named fields.
- Avoid `dynamic` if possible; it is supported (Microsoft.CSharp is referenced).

---

## Lessons from prior PARCS jobs

- Use a final `parallelism=1` layer not only for numerical aggregation, but also for final answer formatting. This prevents accidental client-side arithmetic or prose-level reinterpretation of worker outputs.
- Make worker output schemas explicit before coding. Use small JSON objects with named fields such as `workerIndex`, `start`, `end`, `count`, `sum`, `min`, `max`, and `errors`; this makes the aggregation layer simpler and easier to validate.
- Include lightweight validation data in each partial result when practical, such as processed range bounds or item counts, so the aggregator can detect gaps, overlaps, or failed workers instead of silently combining bad data.
- Keep C# code self-contained and conservative: prefer strongly typed DTOs, `JsonDocument`/`JsonSerializer`, and deterministic partitioning over reflection-heavy or environment-dependent approaches.
- For searches, optimization, Monte Carlo, or statistical jobs, return enough metadata for reproducibility: seed strategy, number of trials/items processed per worker, and selected parameters.
- Before coding, decide the exact JSON contract for both worker outputs and the final aggregation output. Designing the result shape first makes multi-layer jobs easier to test, validate, and present without ad hoc parsing.
- Prefer aggregators that verify completeness explicitly: count worker results, confirm all required partitions were processed, and fail loudly if any worker output is missing or malformed.
- When a user asks for a computed comparison, ranking, or summary, make the final layer emit that exact comparison text or structured JSON instead of returning raw partials for the client to interpret.
- If user asks for reflection or memory updates after a job, capture operational lessons in this skill and behavioral lessons in `/memory/AGENTS.md`; do not store transient job results unless they reveal a reusable pattern.
- For any non-trivial PARCS request, write down the layer plan before touching `create_session`: worker responsibilities, partitioning formula, expected worker JSON, aggregator input expectations, and final output contract.
- Make the aggregation layer validate structure before semantics: check that `results.Length` matches the intended worker count, every `success` flag is true, and every `outputData` payload parses before computing any final answer.
- If a task is small enough that cluster startup and coordination overhead would dominate, say so and either right-size the worker count aggressively or avoid over-parallelising the job.
- When the MCP tool contract evolves, trust the live tool schema over stale examples in the skill text. In particular, `run_layer` resumes with `previousLayerId` rather than an inline previous-result JSON payload, so examples and plans should be written in terms of layer IDs and server-managed result passing.
- Treat the cluster's final layer as responsible for presentable output, but still keep that output machine-checkable when possible: emit concise JSON or exact answer text that can be shown to the user without additional interpretation.
- In recovery planning, preserve resumability explicitly: identify the last successful `layerId`, recompile if needed, and continue from that layer instead of rerunning unaffected earlier layers.
