namespace Parcs.Modules.Hypercube.Models
{
    /// <summary>Main → node, first message: the node's place in the cube and its share of the work.</summary>
    public class NodeSetup
    {
        public int Index { get; set; }

        public int Dimension { get; set; }

        public int ElementsPerNode { get; set; }

        public int Seed { get; set; }
    }

    /// <summary>Node → main, last message: what the node holds after the sort.</summary>
    public class NodeReport
    {
        public int Index { get; set; }

        public int Count { get; set; }

        public int Min { get; set; }

        public int Max { get; set; }

        /// <summary>Sum of the node's sorted block.</summary>
        public long Sum { get; set; }

        /// <summary>Sum of all generated elements, as the all-reduce computed it on this node.</summary>
        public long GlobalSum { get; set; }

        public bool LocallySorted { get; set; }

        public int Exchanges { get; set; }

        public long BytesSent { get; set; }

        public double WiringSeconds { get; set; }

        public double ComputeSeconds { get; set; }
    }

    /// <summary>Written to hypercube_result.json.</summary>
    public class HypercubeResult
    {
        public int Dimension { get; set; }

        public int Nodes { get; set; }

        public int Edges { get; set; }

        public long TotalElements { get; set; }

        public bool Sorted { get; set; }

        public long GlobalSum { get; set; }

        public bool AllReduceAgrees { get; set; }

        public int ExchangesPerNode { get; set; }

        public long BytesExchanged { get; set; }

        public double ElapsedSeconds { get; set; }

        public List<NodeReport> NodeReports { get; set; } = [];
    }
}
