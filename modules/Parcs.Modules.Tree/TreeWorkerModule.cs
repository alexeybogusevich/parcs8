using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Tree.Models;
using Parcs.Net;

namespace Parcs.Modules.Tree
{
    /// <summary>
    /// One node of the tree. Protocol with the main module:
    ///   1. Read NodeSetup.
    ///   2. Open a peer endpoint and send its address; read every node's address.
    ///   3. Dial the parent (sending this node's index first) and accept the children. From here on
    ///      data only flows along tree edges, never through the main module.
    ///   4. Scatter-reduce: take the subtree's range from the parent, pass the children their
    ///      shares, count the primes in the own segment, add up the children's tallies and pass the
    ///      sum to the parent.
    ///   5. Send NodeReport.
    /// </summary>
    public class TreeWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var setup = await moduleInfo.Parent.ReadObjectAsync<NodeSetup>();
            var (index, nodes, arity) = (setup.Index, setup.Nodes, setup.Arity);

            var stopwatch = Stopwatch.StartNew();
            var links = await WireAsync(moduleInfo, setup);
            var wiringSeconds = stopwatch.Elapsed.TotalSeconds;

            moduleInfo.Logger.LogInformation(
                "Tree node {Index}/{Nodes}: parent {Parent}, children [{Children}], wired in {Seconds:F2}s",
                index, nodes, TreeAlgorithms.Parent(index, arity)?.ToString() ?? "none",
                string.Join(", ", TreeAlgorithms.Children(index, nodes, arity)), wiringSeconds);

            stopwatch.Restart();
            var outcome = await TreeAlgorithms.ScatterReduceAsync(
                index, nodes, arity, (2, setup.Limit), links, TreeAlgorithms.CountPrimes);

            await moduleInfo.Parent.WriteObjectAsync(new NodeReport
            {
                Index          = index,
                Depth          = TreeAlgorithms.Depth(index, arity),
                Children       = TreeAlgorithms.Children(index, nodes, arity),
                SegmentStart   = outcome.Segment.Start,
                SegmentEnd     = outcome.Segment.End,
                LocalCount     = outcome.Local.Count,
                SubtreeCount   = outcome.Subtree.Count,
                SubtreeLargest = outcome.Subtree.Largest,
                WiringSeconds  = wiringSeconds,
                ComputeSeconds = stopwatch.Elapsed.TotalSeconds,
            });

            links.Dispose();
        }

        private static async Task<ChannelLinks> WireAsync(IModuleInfo moduleInfo, NodeSetup setup)
        {
            var (index, nodes, arity) = (setup.Index, setup.Nodes, setup.Arity);
            await using var endpoint = await moduleInfo.OpenEndpointAsync();
            await moduleInfo.Parent.WriteDataAsync(endpoint.Address);
            var addresses = await moduleInfo.Parent.ReadObjectAsync<List<string>>();

            var dialParent = Task.Run(async () =>
            {
                if (TreeAlgorithms.Parent(index, arity) is not int parent)
                    return null;
                var channel = await moduleInfo.ConnectAsync(addresses[parent]);
                await channel.WriteDataAsync(index);
                return channel;
            });

            // Children connect in any order; each says who it is.
            var expected = TreeAlgorithms.Children(index, nodes, arity);
            var children = new Dictionary<int, IChannel>();
            foreach (var _ in expected)
            {
                var channel = await endpoint.AcceptAsync();
                var child = await channel.ReadIntAsync();
                if (!expected.Contains(child) || !children.TryAdd(child, channel))
                    throw new InvalidOperationException($"Node {child} is not a child of node {index}.");
            }

            return new ChannelLinks(await dialParent, children);
        }

        private sealed class ChannelLinks(IChannel parent, Dictionary<int, IChannel> children) : ITreeLinks, IDisposable
        {
            public async Task<(long Start, long End)> ReceiveRangeFromParentAsync() =>
                (await parent.ReadLongAsync(), await parent.ReadLongAsync());

            public async Task SendRangeToChildAsync(int child, (long Start, long End) range)
            {
                await children[child].WriteDataAsync(range.Start);
                await children[child].WriteDataAsync(range.End);
            }

            public async Task<PrimeTally> ReceiveTallyFromChildAsync(int child) =>
                new(await children[child].ReadLongAsync(), await children[child].ReadLongAsync());

            public async Task SendTallyToParentAsync(PrimeTally tally)
            {
                await parent.WriteDataAsync(tally.Count);
                await parent.WriteDataAsync(tally.Largest);
            }

            public void Dispose()
            {
                parent?.Dispose();
                foreach (var channel in children.Values)
                    channel.Dispose();
            }
        }
    }
}
