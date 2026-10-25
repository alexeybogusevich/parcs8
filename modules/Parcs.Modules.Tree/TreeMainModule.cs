using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Tree.Models;
using Parcs.Net;

namespace Parcs.Modules.Tree
{
    /// <summary>
    /// Models a K-ary tree of N points. The main module only starts the points and tells them each
    /// other's endpoint addresses; each point connects to its parent itself, so the tree's N-1
    /// edges are direct point-to-point channels. Over the tree the nodes count the primes below
    /// Limit: the range is scattered down (each subtree gets a share proportional to its size),
    /// every node sieves its own segment, and the tallies are reduced back up to the root.
    ///
    /// Arguments: Nodes (default 7), Arity (default 2), Limit (default 100,000,000).
    /// Output: tree_result.json.
    /// </summary>
    public class TreeMainModule : IModule
    {
        private const int MaxNodes = 1024;

        // Up to this bound the main point re-counts with a plain sieve to check the tree's answer.
        private const long SieveCheckLimit = 20_000_000;

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<TreeOptions>();
            if (options.Nodes < 1 || options.Nodes > MaxNodes)
                throw new ArgumentException($"Nodes must be between 1 and {MaxNodes}, not {options.Nodes}.");
            if (options.Arity < 1)
                throw new ArgumentException("Arity must be at least 1.");
            if (options.Limit < 2)
                throw new ArgumentException("Limit must be at least 2.");

            var nodes = options.Nodes;
            moduleInfo.Logger.LogInformation(
                "Tree: {Nodes} nodes, arity {Arity}, primes below {Limit:N0}", nodes, options.Arity, options.Limit);

            var stopwatch = Stopwatch.StartNew();
            var points = await moduleInfo.CreatePointsAsync(nodes);
            var channels = new IChannel[nodes];
            for (var i = 0; i < nodes; i++)
            {
                channels[i] = await points[i].CreateChannelAsync();
            }
            foreach (var point in points)
            {
                await point.ExecuteClassAsync<TreeWorkerModule>();
            }

            try
            {
                for (var i = 0; i < nodes; i++)
                {
                    await channels[i].WriteObjectAsync(new NodeSetup
                    {
                        Index = i,
                        Nodes = nodes,
                        Arity = options.Arity,
                        Limit = options.Limit,
                    });
                }

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
                    "tree_result.json");

                moduleInfo.Logger.LogInformation(
                    "Tree done in {Seconds:F2}s: {Count:N0} primes below {Limit:N0} (largest {Largest:N0}), consistent={Consistent}, sieve check={Sieve}",
                    result.ElapsedSeconds, result.PrimeCount, result.Limit, result.LargestPrime, result.Consistent,
                    result.MatchesSieve?.ToString() ?? "skipped");

                if (!result.Consistent || result.MatchesSieve == false)
                    throw new InvalidOperationException("Tree verification failed; see tree_result.json.");
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

        private static TreeResult Verify(TreeOptions options, List<NodeReport> reports, double elapsedSeconds)
        {
            var root = reports[0];

            // The segments tile [2, Limit) with no gap or overlap, and the root's reduced total is
            // the sum of what every node found on its own.
            var segments = reports.OrderBy(r => r.SegmentStart).ToList();
            var tiled = segments[0].SegmentStart == 2 && segments[^1].SegmentEnd == options.Limit
                && segments.Zip(segments.Skip(1)).All(p => p.First.SegmentEnd == p.Second.SegmentStart);
            var consistent = tiled && reports.Sum(r => r.LocalCount) == root.SubtreeCount;

            bool? matchesSieve = null;
            if (options.Limit <= SieveCheckLimit)
            {
                var expected = TreeAlgorithms.CountPrimes((2, options.Limit));
                matchesSieve = expected.Count == root.SubtreeCount && expected.Largest == root.SubtreeLargest;
            }

            return new TreeResult
            {
                Nodes          = reports.Count,
                Arity          = options.Arity,
                Height         = reports.Max(r => r.Depth),
                Limit          = options.Limit,
                PrimeCount     = root.SubtreeCount,
                LargestPrime   = root.SubtreeLargest,
                Consistent     = consistent,
                MatchesSieve   = matchesSieve,
                ElapsedSeconds = elapsedSeconds,
                NodeReports    = reports,
            };
        }
    }
}
