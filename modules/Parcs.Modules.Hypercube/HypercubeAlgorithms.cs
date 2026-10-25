namespace Parcs.Modules.Hypercube
{
    /// <summary>
    /// Algorithms that only talk across hypercube edges: node i and its partner in dimension k,
    /// i XOR 2^k. Each takes the exchange with a partner as a function, so the same code runs over
    /// peer channels or in memory.
    /// </summary>
    public static class HypercubeAlgorithms
    {
        public static int Partner(int index, int dimension) => index ^ (1 << dimension);

        /// <summary>
        /// Recursive doubling: after one exchange per dimension every node holds the sum over all
        /// 2^d nodes. <paramref name="exchange"/> sends a value to the partner in a dimension and
        /// returns the partner's.
        /// </summary>
        public static async Task<long> AllReduceSumAsync(int dimensions, long value, Func<int, long, Task<long>> exchange)
        {
            for (var k = 0; k < dimensions; k++)
            {
                value += await exchange(k, value);
            }
            return value;
        }

        /// <summary>
        /// Bitonic sort on a hypercube of sorted blocks. Stage s merges bitonic sequences of
        /// 2^(s+1) nodes with one compare-split per dimension s, s-1, ..., 0, so the whole sort is
        /// d(d+1)/2 exchanges per node. Afterwards the blocks are sorted in node order: every
        /// element of node i is at most every element of node i+1. All blocks must have the same
        /// length. Returns the node's final block and the number of exchanges it made.
        /// </summary>
        public static async Task<(int[] Block, int Exchanges)> BitonicSortAsync(
            int index, int dimensions, int[] block, Func<int, int[], Task<int[]>> exchange)
        {
            var current = (int[])block.Clone();
            Array.Sort(current);

            var exchanges = 0;
            for (var stage = 0; stage < dimensions; stage++)
            {
                // Sub-cubes of 2^(stage+1) nodes alternate between ascending and descending order.
                var ascending = ((index >> (stage + 1)) & 1) == 0;
                for (var k = stage; k >= 0; k--)
                {
                    var partner = Partner(index, k);
                    var theirs = await exchange(k, current);
                    current = CompareSplit(current, theirs, keepLow: (index < partner) == ascending);
                    exchanges++;
                }
            }

            return (current, exchanges);
        }

        /// <summary>
        /// Merges two sorted blocks of equal length and keeps the lower or the upper half, sorted.
        /// </summary>
        public static int[] CompareSplit(int[] mine, int[] theirs, bool keepLow)
        {
            if (mine.Length != theirs.Length)
                throw new InvalidOperationException($"Blocks must be the same length ({mine.Length} vs {theirs.Length}).");

            var result = new int[mine.Length];
            if (keepLow)
            {
                int a = 0, b = 0;
                for (var r = 0; r < result.Length; r++)
                    result[r] = b >= theirs.Length || (a < mine.Length && mine[a] <= theirs[b]) ? mine[a++] : theirs[b++];
            }
            else
            {
                int a = mine.Length - 1, b = theirs.Length - 1;
                for (var r = result.Length - 1; r >= 0; r--)
                    result[r] = b < 0 || (a >= 0 && mine[a] >= theirs[b]) ? mine[a--] : theirs[b--];
            }
            return result;
        }
    }
}
