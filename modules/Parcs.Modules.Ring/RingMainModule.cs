using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Ring.Models;
using Parcs.Net;

namespace Parcs.Modules.Ring
{
    /// <summary>
    /// Models a ring of N points, built the way a PARCS configuring module builds a control space:
    /// create the points, link each to the next (LinkPointsAsync, the book's createChannel(p[i],
    /// p[i+1])), then execute them. The ring's N links are direct point-to-point channels that
    /// never pass through the main module. Over the ring the nodes all-reduce a vector
    /// (reduce-scatter, then all-gather) and pass a token around to measure per-hop latency, then
    /// report back for verification.
    ///
    /// Arguments: Nodes (default 4), VectorLength (default 4,000,000), TokenLaps (default 100), Seed (default 42).
    /// Output: ring_result.json.
    /// </summary>
    public class RingMainModule : IModule
    {
        private const int MaxNodes = 1024;

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<RingOptions>();
            if (options.Nodes < 1 || options.Nodes > MaxNodes)
                throw new ArgumentException($"Nodes must be between 1 and {MaxNodes}, not {options.Nodes}.");
            if (options.VectorLength < options.Nodes)
                throw new ArgumentException("VectorLength must be at least Nodes, so every node owns a chunk.");

            var nodes = options.Nodes;
            moduleInfo.Logger.LogInformation(
                "Ring: {Nodes} nodes, vector of {Length:N0}, {Laps} token laps", nodes, options.VectorLength, options.TokenLaps);

            var stopwatch = Stopwatch.StartNew();
            var points = await moduleInfo.CreatePointsAsync(nodes);
            var channels = new IChannel[nodes];
            for (var i = 0; i < nodes; i++)
            {
                channels[i] = await points[i].CreateChannelAsync();
            }

            // Wire the ring before any node starts: node i's "right" is node i+1's "left".
            if (nodes > 1)
            {
                for (var i = 0; i < nodes; i++)
                {
                    await moduleInfo.LinkPointsAsync(points[i], "right", points[RingAlgorithms.Right(i, nodes)], "left");
                }
            }

            foreach (var point in points)
            {
                await point.ExecuteClassAsync<RingWorkerModule>();
            }

            try
            {
                for (var i = 0; i < nodes; i++)
                {
                    await channels[i].WriteObjectAsync(new NodeSetup
                    {
                        Index        = i,
                        Nodes        = nodes,
                        VectorLength = options.VectorLength,
                        TokenLaps    = options.TokenLaps,
                        Seed         = options.Seed,
                    });
                }

                var reports = (await Task.WhenAll(channels.Select(c => c.ReadObjectAsync<NodeReport>())))
                    .OrderBy(r => r.Index)
                    .ToList();
                stopwatch.Stop();

                var result = Verify(options, reports, stopwatch.Elapsed.TotalSeconds);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })),
                    "ring_result.json");

                moduleInfo.Logger.LogInformation(
                    "Ring done in {Seconds:F2}s: all-reduce correct={Correct} in {AllReduce:F2}s, {Bytes:N0} bytes, token lap {Lap:F3} ms",
                    result.ElapsedSeconds, result.AllReduceCorrect, result.AllReduceSeconds, result.BytesExchanged, result.TokenLapMilliseconds);

                if (!result.AllReduceCorrect)
                    throw new InvalidOperationException("Ring all-reduce verification failed; see ring_result.json.");
            }
            finally
            {
                foreach (var point in points)
                {
                    try { await point.DeleteAsync(); } catch { /* best-effort */ }
                }
                foreach (var channel in channels)
                {
                    try { channel.Dispose(); } catch { /* best-effort */ }
                }
            }
        }

        private static RingResult Verify(RingOptions options, List<NodeReport> reports, double elapsedSeconds)
        {
            // The reduced vector sums to the sum of every input, and all nodes hold the same one.
            var expected = reports.Sum(r => r.InputSum);
            var correct = reports.All(r => r.ResultSum == expected && r.ResultChecksum == reports[0].ResultChecksum);
            var lap = reports.FirstOrDefault(r => r.Index == 0)?.TokenLapMilliseconds;

            return new RingResult
            {
                Nodes                = reports.Count,
                VectorLength         = options.VectorLength,
                AllReduceCorrect     = correct,
                ExpectedSum          = expected,
                StepsPerNode         = reports.Count > 0 ? reports.Max(r => r.Steps) : 0,
                BytesExchanged       = reports.Sum(r => r.BytesSent),
                AllReduceSeconds     = reports.Count > 0 ? reports.Max(r => r.AllReduceSeconds) : 0,
                TokenLaps            = options.TokenLaps,
                TokenLapMilliseconds = lap,
                HopMicroseconds      = lap * 1000 / reports.Count,
                ElapsedSeconds       = elapsedSeconds,
                NodeReports          = reports,
            };
        }
    }
}
