using Parcs.Modules.Tree.Models;

namespace Parcs.Modules.Tree
{
    /// <summary>A node's links to its parent and children, over peer channels or in memory.</summary>
    public interface ITreeLinks
    {
        Task<(long Start, long End)> ReceiveRangeFromParentAsync();

        Task SendRangeToChildAsync(int child, (long Start, long End) range);

        Task<PrimeTally> ReceiveTallyFromChildAsync(int child);

        Task SendTallyToParentAsync(PrimeTally tally);
    }

    /// <summary>What one node did in a scatter-reduce.</summary>
    public record TreeOutcome((long Start, long End) Segment, PrimeTally Local, PrimeTally Subtree);

    /// <summary>
    /// K-ary tree algorithms: node 0 is the root, node i's parent is (i-1)/K and its children are
    /// K·i+1 … K·i+K. Nodes only talk to their parent and children.
    /// </summary>
    public static class TreeAlgorithms
    {
        public static int? Parent(int index, int arity) => index == 0 ? null : (index - 1) / arity;

        public static List<int> Children(int index, int nodes, int arity) =>
            [.. Enumerable.Range(arity * index + 1, arity).Where(c => c < nodes)];

        public static int Depth(int index, int arity)
        {
            var depth = 0;
            for (var i = index; i > 0; i = (i - 1) / arity)
                depth++;
            return depth;
        }

        public static int SubtreeSize(int index, int nodes, int arity) =>
            1 + Children(index, nodes, arity).Sum(c => SubtreeSize(c, nodes, arity));

        /// <summary>
        /// Splits a subtree's range into the node's own segment (first) and one range per child,
        /// each in proportion to the number of nodes it covers, so every node gets about the same
        /// amount of work however unbalanced the tree is.
        /// </summary>
        public static ((long Start, long End) Own, List<(int Child, (long Start, long End) Range)> Children) Split(
            int index, int nodes, int arity, (long Start, long End) range)
        {
            var childList = Children(index, nodes, arity);
            var shares = new List<int> { 1 };
            shares.AddRange(childList.Select(c => SubtreeSize(c, nodes, arity)));
            var total = shares.Sum();
            var length = range.End - range.Start;

            // Boundary j sits after the first j shares; the last one is the range end, so the parts tile exactly.
            var boundaries = new long[shares.Count + 1];
            boundaries[0] = range.Start;
            for (int j = 1, covered = 0; j <= shares.Count; j++)
            {
                covered += shares[j - 1];
                boundaries[j] = j == shares.Count ? range.End : range.Start + (long)((decimal)length * covered / total);
            }

            var own = (boundaries[0], boundaries[1]);
            var children = childList.Select((child, c) => (child, (boundaries[c + 1], boundaries[c + 2]))).ToList();
            return (own, children);
        }

        /// <summary>
        /// One node's part of the scatter-reduce: take the subtree's range from the parent (the root
        /// gets <paramref name="rootRange"/>), hand each child its share, work on the own segment,
        /// then add up the children's tallies and pass the sum to the parent.
        /// </summary>
        public static async Task<TreeOutcome> ScatterReduceAsync(
            int index, int nodes, int arity, (long Start, long End) rootRange, ITreeLinks links,
            Func<(long Start, long End), PrimeTally> work)
        {
            var range = index == 0 ? rootRange : await links.ReceiveRangeFromParentAsync();
            var (own, children) = Split(index, nodes, arity, range);

            foreach (var (child, childRange) in children)
                await links.SendRangeToChildAsync(child, childRange);

            var local = work(own);
            var subtree = local;
            foreach (var (child, _) in children)
                subtree += await links.ReceiveTallyFromChildAsync(child);

            if (index != 0)
                await links.SendTallyToParentAsync(subtree);

            return new TreeOutcome(own, local, subtree);
        }

        /// <summary>Counts the primes in [start, end) with a segmented sieve.</summary>
        public static PrimeTally CountPrimes((long Start, long End) range)
        {
            var (start, end) = (Math.Max(range.Start, 2), range.End);
            if (start >= end)
                return default;

            var basePrimes = SmallPrimes((int)Math.Sqrt(end) + 1);
            const int window = 1 << 20;
            var composite = new bool[window];
            long count = 0, largest = 0;

            for (var low = start; low < end; low += window)
            {
                var high = Math.Min(low + window, end);
                Array.Clear(composite);
                foreach (var p in basePrimes)
                {
                    if ((long)p * p >= high)
                        break;
                    var first = Math.Max((long)p * p, (low + p - 1) / p * p);
                    for (var m = first; m < high; m += p)
                        composite[m - low] = true;
                }
                for (var n = low; n < high; n++)
                {
                    if (!composite[n - low])
                    {
                        count++;
                        largest = n;
                    }
                }
            }

            return new PrimeTally(count, largest);
        }

        private static List<int> SmallPrimes(int limit)
        {
            var composite = new bool[limit + 1];
            var primes = new List<int>();
            for (var i = 2; i <= limit; i++)
            {
                if (composite[i])
                    continue;
                primes.Add(i);
                for (var m = (long)i * i; m <= limit; m += i)
                    composite[m] = true;
            }
            return primes;
        }
    }
}
