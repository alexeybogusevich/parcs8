namespace Parcs.Modules.Ring
{
    /// <summary>
    /// Ring algorithms: node i sends only to its right neighbour (i+1) and receives only from its
    /// left one (i-1). The step with the neighbours is passed in as a function (send to the right,
    /// return what came from the left), so the same code runs over peer channels or in memory.
    /// </summary>
    public static class RingAlgorithms
    {
        public static int Right(int index, int nodes) => (index + 1) % nodes;

        public static int Left(int index, int nodes) => (index - 1 + nodes) % nodes;

        /// <summary>
        /// Ring all-reduce (sum): the vector is cut into one chunk per node. In n-1 reduce-scatter
        /// steps every chunk travels around the ring collecting partial sums, so node i ends up
        /// owning the complete chunk i+1; in n-1 all-gather steps the complete chunks travel
        /// around once more. Every node sends 2(n-1)/n of the vector in total, however many nodes
        /// there are, which is what makes the ring the bandwidth-optimal all-reduce.
        /// Returns the reduced vector and the number of steps.
        /// </summary>
        public static async Task<(long[] Vector, int Steps)> AllReduceSumAsync(
            int index, int nodes, long[] vector, Func<long[], Task<long[]>> shiftRight)
        {
            var result = (long[])vector.Clone();
            if (nodes == 1)
                return (result, 0);

            var steps = 0;
            for (var s = 0; s < nodes - 1; s++)
            {
                var send = Mod(index - s, nodes);
                var receive = Mod(index - s - 1, nodes);
                var incoming = await shiftRight(Chunk(result, send, nodes));
                var (start, _) = Bounds(result.Length, receive, nodes);
                for (var k = 0; k < incoming.Length; k++)
                    result[start + k] += incoming[k];
                steps++;
            }

            for (var s = 0; s < nodes - 1; s++)
            {
                var send = Mod(index + 1 - s, nodes);
                var receive = Mod(index - s, nodes);
                var incoming = await shiftRight(Chunk(result, send, nodes));
                var (start, _) = Bounds(result.Length, receive, nodes);
                incoming.CopyTo(result, start);
                steps++;
            }

            return (result, steps);
        }

        /// <summary>Start and length of chunk <paramref name="chunk"/> when <paramref name="length"/> is cut into <paramref name="nodes"/> near-equal parts.</summary>
        public static (int Start, int Length) Bounds(int length, int chunk, int nodes)
        {
            var start = (int)((long)length * chunk / nodes);
            var end = (int)((long)length * (chunk + 1) / nodes);
            return (start, end - start);
        }

        /// <summary>Order-sensitive checksum, so two nodes agree only on equal vectors.</summary>
        public static ulong Checksum(long[] vector)
        {
            var hash = 14695981039346656037UL;
            foreach (var value in vector)
            {
                hash = (hash ^ (ulong)value) * 1099511628211UL;
            }
            return hash;
        }

        private static long[] Chunk(long[] vector, int chunk, int nodes)
        {
            var (start, length) = Bounds(vector.Length, chunk, nodes);
            return vector[start..(start + length)];
        }

        private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
    }
}
