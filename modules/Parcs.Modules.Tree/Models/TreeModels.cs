using Parcs.Net;

namespace Parcs.Modules.Tree.Models
{
    public class TreeOptions : IModuleOptions
    {
        /// <summary>Points in the tree.</summary>
        public int Nodes { get; set; } = 7;

        /// <summary>Children per node: node i's parent is (i-1)/Arity.</summary>
        public int Arity { get; set; } = 2;

        /// <summary>The tree counts the primes below this bound.</summary>
        public long Limit { get; set; } = 100_000_000;
    }

    /// <summary>Main → node, first message.</summary>
    public class NodeSetup
    {
        public int Index { get; set; }

        public int Nodes { get; set; }

        public int Arity { get; set; }

        public long Limit { get; set; }
    }

    /// <summary>Primes counted in a range, and the largest of them (0 when there is none).</summary>
    public record struct PrimeTally(long Count, long Largest)
    {
        public static PrimeTally operator +(PrimeTally a, PrimeTally b) => new(a.Count + b.Count, Math.Max(a.Largest, b.Largest));
    }

    /// <summary>Node → main, last message.</summary>
    public class NodeReport
    {
        public int Index { get; set; }

        public int Depth { get; set; }

        public List<int> Children { get; set; } = [];

        /// <summary>The part of [2, Limit) this node searched itself.</summary>
        public long SegmentStart { get; set; }

        public long SegmentEnd { get; set; }

        public long LocalCount { get; set; }

        /// <summary>Primes found in this node's whole subtree, as reduced up to it.</summary>
        public long SubtreeCount { get; set; }

        public long SubtreeLargest { get; set; }

        public double WiringSeconds { get; set; }

        public double ComputeSeconds { get; set; }
    }

    /// <summary>Written to tree_result.json.</summary>
    public class TreeResult
    {
        public int Nodes { get; set; }

        public int Arity { get; set; }

        public int Height { get; set; }

        public long Limit { get; set; }

        public long PrimeCount { get; set; }

        public long LargestPrime { get; set; }

        /// <summary>The segments cover [2, Limit) exactly once and the local counts add up to the root's total.</summary>
        public bool Consistent { get; set; }

        /// <summary>Whether a sieve on the main point agreed; null when Limit is too large to check that way.</summary>
        public bool? MatchesSieve { get; set; }

        public double ElapsedSeconds { get; set; }

        public List<NodeReport> NodeReports { get; set; } = [];
    }
}
