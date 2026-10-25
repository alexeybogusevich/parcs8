using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Hypercube.Models;
using Parcs.Net;

namespace Parcs.Modules.Hypercube
{
    /// <summary>
    /// One node of the hypercube. Protocol with the main module:
    ///   1. Read NodeSetup.
    ///   2. Open a peer endpoint and send its address; read every node's address.
    ///   3. Wire the d edges: dial the partners with a higher index (sending this node's index
    ///      first), accept the ones with a lower index. No data goes through the main module.
    ///   4. All-reduce the sum of the generated data, then bitonic-sort it across the cube.
    ///   5. Send NodeReport.
    /// </summary>
    public class HypercubeWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var setup = await moduleInfo.Parent.ReadObjectAsync<NodeSetup>();
            var index = setup.Index;

            var stopwatch = Stopwatch.StartNew();
            var edges = await WireEdgesAsync(moduleInfo, setup);
            var wiringSeconds = stopwatch.Elapsed.TotalSeconds;

            moduleInfo.Logger.LogInformation(
                "Hypercube node {Index}/{Nodes} wired {Edges} edges in {Seconds:F2}s",
                index, 1 << setup.Dimension, edges.Length, wiringSeconds);

            var random = new Random(setup.Seed + index);
            var data = new int[setup.ElementsPerNode];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = random.Next();
            }

            long bytesSent = 0;
            stopwatch.Restart();

            var globalSum = await HypercubeAlgorithms.AllReduceSumAsync(setup.Dimension, data.Sum(x => (long)x), async (k, value) =>
            {
                var send = edges[k].WriteDataAsync(value).AsTask();
                var received = await edges[k].ReadLongAsync();
                await send;
                bytesSent += sizeof(long);
                return received;
            });

            var (block, exchanges) = await HypercubeAlgorithms.BitonicSortAsync(index, setup.Dimension, data, async (k, mine) =>
            {
                // Send and receive at once: both partners send a whole block, which would fill the
                // socket buffers if each waited for its own send to finish first.
                var bytes = new byte[mine.Length * sizeof(int)];
                Buffer.BlockCopy(mine, 0, bytes, 0, bytes.Length);
                var send = edges[k].WriteDataAsync(bytes).AsTask();
                var received = await edges[k].ReadBytesAsync();
                await send;
                bytesSent += bytes.Length;

                var theirs = new int[received.Length / sizeof(int)];
                Buffer.BlockCopy(received, 0, theirs, 0, received.Length);
                return theirs;
            });

            var computeSeconds = stopwatch.Elapsed.TotalSeconds;

            await moduleInfo.Parent.WriteObjectAsync(new NodeReport
            {
                Index          = index,
                Count          = block.Length,
                Min            = block.Length > 0 ? block[0] : 0,
                Max            = block.Length > 0 ? block[^1] : 0,
                Sum            = block.Sum(x => (long)x),
                GlobalSum      = globalSum,
                LocallySorted  = IsSorted(block),
                Exchanges      = exchanges,
                BytesSent      = bytesSent,
                WiringSeconds  = wiringSeconds,
                ComputeSeconds = computeSeconds,
            });

            foreach (var edge in edges)
            {
                edge.Dispose();
            }
        }

        /// <summary>Returns the channel to the partner in each dimension, indexed by dimension.</summary>
        private static async Task<IChannel[]> WireEdgesAsync(IModuleInfo moduleInfo, NodeSetup setup)
        {
            var index = setup.Index;
            await using var endpoint = await moduleInfo.OpenEndpointAsync();
            await moduleInfo.Parent.WriteDataAsync(endpoint.Address);
            var addresses = await moduleInfo.Parent.ReadObjectAsync<List<string>>();

            var dimensions = Enumerable.Range(0, setup.Dimension).ToList();
            var dialed = dimensions
                .Where(k => HypercubeAlgorithms.Partner(index, k) > index)
                .Select(async k =>
                {
                    var channel = await moduleInfo.ConnectAsync(addresses[HypercubeAlgorithms.Partner(index, k)]);
                    await channel.WriteDataAsync(index);
                    return (Dimension: k, Channel: channel);
                });
            var accepted = dimensions
                .Where(k => HypercubeAlgorithms.Partner(index, k) < index)
                .Select(async _ =>
                {
                    // Lower partners connect in any order; each says who it is.
                    var channel = await endpoint.AcceptAsync();
                    var partner = await channel.ReadIntAsync();
                    var dimension = System.Numerics.BitOperations.Log2((uint)(partner ^ index));
                    if (HypercubeAlgorithms.Partner(index, dimension) != partner)
                        throw new InvalidOperationException($"Node {partner} is not a hypercube neighbour of node {index}.");
                    return (Dimension: dimension, Channel: channel);
                });

            var edges = new IChannel[setup.Dimension];
            foreach (var (dimension, channel) in await Task.WhenAll(dialed.Concat(accepted)))
            {
                edges[dimension] = channel;
            }
            return edges;
        }

        private static bool IsSorted(int[] block)
        {
            for (var i = 1; i < block.Length; i++)
            {
                if (block[i - 1] > block[i])
                    return false;
            }
            return true;
        }
    }
}
