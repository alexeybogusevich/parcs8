using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Modules.Ring.Models;
using Parcs.Net;

namespace Parcs.Modules.Ring
{
    /// <summary>
    /// One node of the ring. Protocol with the main module:
    ///   1. Read NodeSetup.
    ///   2. Take the "left" and "right" links the main module set up before this node started.
    ///      From here on data only flows around the ring, never through the main module.
    ///   3. All-reduce a vector with the ring algorithm, then pass a token around TokenLaps times.
    ///   4. Send NodeReport.
    /// </summary>
    public class RingWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var setup = await moduleInfo.Parent.ReadObjectAsync<NodeSetup>();
            var (index, nodes) = (setup.Index, setup.Nodes);

            var stopwatch = Stopwatch.StartNew();
            var (left, right) = await WireAsync(moduleInfo, setup);
            var wiringSeconds = stopwatch.Elapsed.TotalSeconds;

            moduleInfo.Logger.LogInformation(
                "Ring node {Index}/{Nodes}: left {Left}, right {Right}, wired in {Seconds:F2}s",
                index, nodes, RingAlgorithms.Left(index, nodes), RingAlgorithms.Right(index, nodes), wiringSeconds);

            var random = new Random(setup.Seed + index);
            var vector = new long[setup.VectorLength];
            for (var k = 0; k < vector.Length; k++)
            {
                vector[k] = random.Next(0, 1000);
            }

            long bytesSent = 0;
            stopwatch.Restart();
            var (result, steps) = await RingAlgorithms.AllReduceSumAsync(index, nodes, vector, async chunk =>
            {
                // Send right and receive left at once: every node sends in the same step, so
                // waiting for the own send first could fill the socket buffers all around the ring.
                var bytes = new byte[chunk.Length * sizeof(long)];
                Buffer.BlockCopy(chunk, 0, bytes, 0, bytes.Length);
                var send = right.WriteDataAsync(bytes).AsTask();
                var received = await left.ReadBytesAsync();
                await send;
                bytesSent += bytes.Length;

                var incoming = new long[received.Length / sizeof(long)];
                Buffer.BlockCopy(received, 0, incoming, 0, received.Length);
                return incoming;
            });
            var allReduceSeconds = stopwatch.Elapsed.TotalSeconds;

            double? lapMilliseconds = null;
            if (nodes > 1 && setup.TokenLaps > 0)
            {
                stopwatch.Restart();
                for (var lap = 0; lap < setup.TokenLaps; lap++)
                {
                    if (index == 0)
                    {
                        await right.WriteDataAsync(lap);
                        var back = await left.ReadIntAsync();
                        if (back != lap)
                            throw new InvalidOperationException($"Token {back} came back on lap {lap}.");
                    }
                    else
                    {
                        await right.WriteDataAsync(await left.ReadIntAsync());
                    }
                }
                if (index == 0)
                    lapMilliseconds = stopwatch.Elapsed.TotalMilliseconds / setup.TokenLaps;
            }

            await moduleInfo.Parent.WriteObjectAsync(new NodeReport
            {
                Index                = index,
                InputSum             = vector.Sum(),
                ResultSum            = result.Sum(),
                ResultChecksum       = RingAlgorithms.Checksum(result),
                Steps                = steps,
                BytesSent            = bytesSent,
                WiringSeconds        = wiringSeconds,
                AllReduceSeconds     = allReduceSeconds,
                TokenLapMilliseconds = lapMilliseconds,
            });

            left?.Dispose();
            right?.Dispose();
        }

        /// <summary>
        /// Channels to the left and right neighbours (null for a ring of one). The main module
        /// linked them before this node started; in a ring of two, left and right are the same
        /// node but still two channels, one per direction.
        /// </summary>
        private static async Task<(IChannel Left, IChannel Right)> WireAsync(IModuleInfo moduleInfo, NodeSetup setup)
        {
            if (setup.Nodes == 1)
                return (null, null);

            return (await moduleInfo.GetLinkAsync("left"), await moduleInfo.GetLinkAsync("right"));
        }
    }
}
