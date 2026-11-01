"""System-prompt guidance the middlewares add.

The tool descriptions carry the API contract (arguments, envelope, error codes); this text only
says how to work with it. ``PTC`` is a template with an ``{eval_tool}`` field.
"""

SHARED = """\
## PARCS cluster

You can run C# code in parallel on a PARCS cluster: a session holds `IAgentComputation` classes, and a
**layer** runs one of them on N workers. A later layer reads earlier layers' results through
`previousLayerIds`. Every PARCS tool returns one envelope `{ok, status, ..., output, error}`; `ok` is
false exactly when `error` is set, and `status` is `Queued`/`Running` (wait), `Completed`,
`CompletedWithFailures`, `Failed` or `Cancelled`.

- **Compute, never fabricate.** Every numerical claim in your answer must come from a layer you ran.
- **Size to the cluster.** Take the worker count from `get_cluster_info` and never exceed its
  `maxParallelism`; it is cheap and answers in under a second. A worker
  count in the task is a number of logical shards: run them on `min(shards, maxParallelism)` workers
  in ONE layer, each worker looping over its shards.
- **One layer, not a sequence of batches.** Every layer has a fixed overhead of seconds.
- **Return small outputs.** Reduce inside the worker (counts, sums, histograms, bounded top-K); raw
  samples make the reducer fail with `out_of_memory`.
- **Final answer from the cluster.** Do aggregation in a final `parallelism: 1` layer and report its
  `output`.
- **Give every layer a stable `layerKey`.** Repeating a call with the same key returns the finished
  layer instead of running it again, so after a timeout or a failed script just repeat it. Repeat a
  call unchanged only when `error.retryable` is true; otherwise fix the cause first.
- Workers have no network or file access; pass small inputs in `customData`, `parameters` or
  `workerInputs`.
- **Never fall back to one worker running the whole job.** If a parallel layer fails, read
  `error`/`diagnostics`, fix that cause and rerun the parallel layer; a single-worker rerun of the
  full workload is slower than the time you have and will be cancelled.

### C# worker contract

Write against exactly this API. Do not guess other member names and do not probe for them by trial
compiles; fix compile errors from `error.diagnostics`.

```csharp
using System; using System.Collections.Generic; using System.Linq;
using System.Text.Json; using System.Threading; using System.Threading.Tasks;
using Parcs.Agent.Runtime;

public class MyJob : IAgentComputation
{
    public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct)
    {
        // input.WorkerIndex, input.TotalWorkers, input.CustomData, input.WorkerInput,
        // input.Parameters (Dictionary<string,string>), input.GetPreviousOutputs(parentIndex = 0)
        return Task.FromResult(AgentLayerResult.Ok(JsonSerializer.Serialize(new { count = 1 })));
    }
}
```

- The method is `Task<AgentLayerResult> ExecuteAsync(AgentLayerInput, CancellationToken)`; there is
  no `Compute`, `AgentContext`, `WorkerCount`, or `Task<object>`.
- `AgentLayerResult` is built only with `AgentLayerResult.Ok(string json)` (a method call) or
  `AgentLayerResult.Error(string message)`. It has no constructor arguments and no `Output`, `Result`
  or `Value` member. The payload is a **string**: serialize it with `JsonSerializer.Serialize(...)`.
- Shard the work with `for (int s = input.WorkerIndex; s < shards; s += input.TotalWorkers)`.
- A later layer reads its parents with `input.GetPreviousOutputs()`: one string per worker of that parent
  (the string it passed to `Ok`), in worker order, `null` for a failed worker. Skip nulls and
  `JsonSerializer.Deserialize<T>` each string. Do not parse `PreviousLayerResults` yourself: those are
  whole-layer documents, not your payloads, and deserializing them as your type leaves its fields null.
- Result types you pass to `JsonSerializer` must be plain: public properties with `{ get; set; }`
  (or an anonymous type to write, and a class with matching property names to read), and **no
  constructor with parameters**. A class like `class R { public double[] x; public R(double[] a) {...} }`
  serializes but fails in the reduce layer with "Each parameter in the deserialization constructor ...
  must bind to an object property", after the expensive map layer has already run. Define the type once
  and use it in both the map and the reduce class. `double[]` and `List<double>` are fine.
- Workers are small (a fraction of a CPU core). Keep hot loops tight: to keep the top K of a stream,
  collect the values, sort descending and take K once per shard; never scan K items for every sample.
"""

TOOL_CALLING = """\
### Driving PARCS with tool calls

Call the PARCS tools yourself, one call per step, and read each envelope before the next.

1. `get_cluster_info` to choose `parallelism`.
2. `create_session` with the complete C# source; on `compile_error`, fix the lines in
   `error.diagnostics` and call it again.
3. `run_layer` for each layer. While the status is `Running` or `Queued`, call `wait_layer(layerId)`;
   start a dependent layer only after its parent is `Completed`.
4. Keep `view` at the default summary unless you need per-worker outputs or errors.
"""

PTC = """### Driving PARCS from code

The PARCS tools are not direct tool calls. They are functions on the `tools` object inside the
`{eval_tool}` tool: `tools.getClusterInfo({{}})`, `tools.createSession({{ sourceCode }})`,
`tools.runLayer({{...}})`, `tools.waitLayer({{...}})` (camelCase of the tool name). Code in `{eval_tool}` is
a program you write once and the runtime executes without you: it can call any number of tools, loop,
branch and pass values between calls in plain JavaScript, and only the value of its last expression
comes back to you.

**Write the whole computation as ONE `{eval_tool}` program.** Every `{eval_tool}` call costs a model turn;
JavaScript between tool calls is free. So plan the orchestration before you write it, then submit it
in one go: size the cluster, `createSession` with every class the computation needs (map and reduce
classes go in one source, one session), run the map layer, feed its `layerId` to the reduce layer
through `previousLayerIds`, and return the final answer. Do not return to the model between steps that
need no decision from it:

- Size the cluster inside the program too. Never spend an `{eval_tool}` call just to run
  `getClusterInfo`: call it on the first line, compute the worker count in arithmetic, e.g.
  `const workers = Math.min(shards, info.output.maxParallelism)`, and use it for the layers.
- Chain dependent calls inside the program. A `layerId` or `sessionId` from one call goes straight
  into the next; do not print it, wait for your next turn, and paste it back as a literal.
- Run independent layers with `Promise.all` instead of one after another.
- Use `console.log` or the last expression only for what you need to read afterwards: the final
  answer, or a compact summary of a failure. Keep big intermediate data in JS variables.
- Split into several `{eval_tool}` calls only when the next step needs your judgment on what you have
  just seen: a compile error you must fix, or a result that decides what to compute next. After a
  failed program, fix the cause and resend the whole program, not just the failing step; unchanged
  `layerKey`s return the layers that already finished, so nothing is recomputed. Names declared at top
  level persist between calls, so reference them instead of retyping their values.

Each tool call resolves to an array of content blocks, not to the envelope: the envelope is
`JSON.parse(r[0].text)`. Reading `.ok` or `.output` straight off `r` gives `undefined`, so `if (!r.ok)
throw ...` would fire on every call, even a successful one, with an empty message. Declare these
helpers at the top of the program and use them for every call:

```javascript
{{
  const call = async (fn, args) => {{
    const r = JSON.parse((await fn(args))[0].text);
    if (!r.ok) throw new Error(JSON.stringify(r.error));
    return r;
  }};
  const run = async (args) => {{   // runLayer, then wait until the layer has finished
    let r = await call(tools.runLayer, {{ waitSeconds: 200, ...args }});
    while (r.status === 'Running' || r.status === 'Queued')
      r = await call(tools.waitLayer, {{ layerId: r.layerId, waitSeconds: 200 }});
    return r;   // 'Completed', or 'CompletedWithFailures': check layer.failureCount
  }};

  const info = await call(tools.getClusterInfo, {{}});
  const shards = 20;   // logical work units from the task
  const workers = Math.min(shards, info.output.maxParallelism);
  const src = String.raw`/* C#: class Map and class Reduce, both IAgentComputation */`;
  const {{ sessionId }} = await call(tools.createSession, {{ sourceCode: src }});
  const map = await run({{ sessionId, entryPoint: 'Map', parallelism: workers, layerKey: 'map-1' }});
  const red = await run({{ sessionId, entryPoint: 'Reduce', parallelism: 1, layerKey: 'reduce-1',
                          previousLayerIds: [map.layerId] }});
  red.output;   // the answer; the last expression is what you get back
}}
```

- Put the C# source in a `String.raw` template literal (no backtick or `${{` inside it) and keep it
  compact. If a program fails with `Unterminated string`, it was cut off: shorten the C# and resend
  the whole program.
- Wrap the program in `{{ ... }}` so its `const`s can be redeclared in a later call.
- Never throw on capacity: derive `parallelism` from `getClusterInfo`.
- Run shards in one layer, not in a JavaScript loop of layers.
- A failure always names its cause (a compile diagnostic, a worker error, or a mistake in your own
  script). Fix that cause rather than giving up or substituting an approximation.
"""
