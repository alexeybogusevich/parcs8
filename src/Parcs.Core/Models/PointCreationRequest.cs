namespace Parcs.Core.Models
{
    public class PointCreationRequest
    {
        public long JobId { get; set; }

        public long ModuleId { get; set; }

        public IDictionary<string, string> Arguments { get; set; }

        /// <summary>Address of the parent point (the Host or a daemon) to connect back to.</summary>
        public string HostUrl { get; set; }

        public int HostPort { get; set; }

        public string CorrelationId { get; set; }

        /// <summary>
        /// Whether the job runs on the GPU pool. Carried in the message so that a daemon creating
        /// nested points routes them to the same pool without access to the job database.
        /// </summary>
        public bool RequiresGpu { get; set; }

        /// <summary>
        /// UTC time the request was published. Lets the daemon log queue-to-start latency
        /// (KEDA polling + pod scheduling + node provisioning) for scaling experiments.
        /// </summary>
        public DateTimeOffset? RequestedAt { get; set; }
    }
}
