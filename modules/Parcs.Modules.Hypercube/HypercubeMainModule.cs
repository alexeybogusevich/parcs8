using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Hypercube.Models;
using Parcs.Net;

namespace Parcs.Modules.Hypercube
{
    /// <summary>
    /// Models a d-dimensional hypercube of 2^d points. The main module only starts the points and
    /// tells them each other's endpoint addresses; the points connect to their d neighbours
    /// (i XOR 2^k) themselves, so the cube's d·2^(d-1) edges are direct point-to-point channels.
    /// Over those edges the nodes all-reduce a sum by recursive doubling and bitonic-sort
    /// 2^d · ElementsPerNode random integers, then report back for verification.
    ///
    /// Arguments: Dimension (default 3), ElementsPerNode (default 1,000,000), Seed (default 42).
    /// Output: hypercube_result.json.
    /// </summary>
    public class HypercubeMainModule : IModule
    {
        private const int MaxDimension = 10;

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<HypercubeOptions>();
            if (options.Dimension < 0 || options.Dimension > MaxDimension)
                throw new ArgumentException($"Dimension must be between 0 and {MaxDimension}, not {options.Dimension}.");
            if (options.ElementsPerNode < 1)
                throw new ArgumentException("ElementsPerNode must be at least 1.");

            var nodes = 1 << options.Dimension;
            moduleInfo.Logger.LogInformation(
                "Hypercube: dimension {Dimension}, {Nodes} nodes, {Elements:N0} elements per node",
                options.Dimension, nodes, options.ElementsPerNode);

            var stopwatch = Stopwatch.StartNew();
            var points = await moduleInfo.CreatePointsAsync(nodes);
            var channels = new IChannel[nodes];
            for (var i = 0; i < nodes; i++)
            {
                channels[i] = await points[i].CreateChannelAsync();
            }
            foreach (var point in points)
            {
                await point.ExecuteClassAsync<HypercubeWorkerModule>();
            }

            try
            {
                for (var i = 0; i < nodes; i++)
                {
                    await channels[i].WriteObjectAsync(new NodeSetup
                    {
                        Index           = i,
                        Dimension       = options.Dimension,
                        ElementsPerNode = options.ElementsPerNode,
                        Seed            = options.Seed,
                    });
                }

                // Every node needs only its d neighbours' addresses, but the whole list is small.
                var addresses = (await Task.WhenAll(channels.Select(c => c.ReadStringAsync()))).ToList();
                foreach (var channel in channels)
                {
                    await channel.WriteObjectAsync(addresses);
                }

                var reports = (await Task.WhenAll(channels.Select(c => c.ReadObjectAsync<NodeReport>())))
                    .OrderBy(r => r.Index)
                    .ToList();
                stopwatch.Stop();

                var result = Verify(options, reports, stopwatch.Elapsed.TotalSeconds);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })),
                    "hypercube_result.json");

                moduleInfo.Logger.LogInformation(
                    "Hypercube done in {Seconds:F2}s: sorted={Sorted}, all-reduce agrees={AllReduce}, {Exchanges} exchanges per node, {Bytes:N0} bytes over {Edges} edges",
                    result.ElapsedSeconds, result.Sorted, result.AllReduceAgrees, result.ExchangesPerNode, result.BytesExchanged, result.Edges);

                if (!result.Sorted || !result.AllReduceAgrees)
                    throw new InvalidOperationException("Hypercube verification failed; see hypercube_result.json.");
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

        private static HypercubeResult Verify(HypercubeOptions options, List<NodeReport> reports, double elapsedSeconds)
        {
            var nodes = reports.Count;
            var totalSum = reports.Sum(r => r.Sum);

            // Sorted across the cube: each block is sorted and no block starts below the previous one's end.
            var sorted = reports.All(r => r.LocallySorted && r.Count == options.ElementsPerNode)
                && reports.Zip(reports.Skip(1)).All(pair => pair.First.Max <= pair.Second.Min);

            return new HypercubeResult
            {
                Dimension        = options.Dimension,
                Nodes            = nodes,
                Edges            = options.Dimension * nodes / 2,
                TotalElements    = (long)nodes * options.ElementsPerNode,
                Sorted           = sorted,
                GlobalSum        = totalSum,
                // Every node computed the global sum on its own; the sort only moved elements around.
                AllReduceAgrees  = reports.All(r => r.GlobalSum == totalSum),
                ExchangesPerNode = reports.Count > 0 ? reports.Max(r => r.Exchanges) : 0,
                BytesExchanged   = reports.Sum(r => r.BytesSent),
                ElapsedSeconds   = elapsedSeconds,
                NodeReports      = reports,
            };
        }
    }
}
