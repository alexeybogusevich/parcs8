using Parcs.Net;

namespace Parcs.Modules.Ring.Models
{
    public class RingOptions : IModuleOptions
    {
        /// <summary>Points in the ring.</summary>
        public int Nodes { get; set; } = 4;

        /// <summary>Length of the vector every node contributes to the all-reduce.</summary>
        public int VectorLength { get; set; } = 4_000_000;

        /// <summary>Times the token goes around the ring for the latency measurement.</summary>
        public int TokenLaps { get; set; } = 100;

        public int Seed { get; set; } = 42;
    }

    /// <summary>Main → node, first message.</summary>
    public class NodeSetup
    {
        public int Index { get; set; }

        public int Nodes { get; set; }

        public int VectorLength { get; set; }

        public int TokenLaps { get; set; }

        public int Seed { get; set; }
    }

    /// <summary>Node → main, last message.</summary>
    public class NodeReport
    {
        public int Index { get; set; }

        /// <summary>Sum of the vector this node contributed.</summary>
        public long InputSum { get; set; }

        /// <summary>Sum of the all-reduced vector this node ended with.</summary>
        public long ResultSum { get; set; }

        /// <summary>Order-sensitive checksum of the all-reduced vector; equal on every node when they agree.</summary>
        public ulong ResultChecksum { get; set; }

        public int Steps { get; set; }

        public long BytesSent { get; set; }

        public double WiringSeconds { get; set; }

        public double AllReduceSeconds { get; set; }

        /// <summary>Average time for one token lap, measured on node 0 only.</summary>
        public double? TokenLapMilliseconds { get; set; }
    }

    /// <summary>Written to ring_result.json.</summary>
    public class RingResult
    {
        public int Nodes { get; set; }

        public int VectorLength { get; set; }

        public bool AllReduceCorrect { get; set; }

        public long ExpectedSum { get; set; }

        public int StepsPerNode { get; set; }

        public long BytesExchanged { get; set; }

        public double AllReduceSeconds { get; set; }

        public int TokenLaps { get; set; }

        public double? TokenLapMilliseconds { get; set; }

        public double? HopMicroseconds { get; set; }

        public double ElapsedSeconds { get; set; }

        public List<NodeReport> NodeReports { get; set; } = [];
    }
}
