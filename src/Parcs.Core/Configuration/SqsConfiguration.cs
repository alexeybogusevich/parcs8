namespace Parcs.Core.Configuration
{
    /// <summary>
    /// Configuration for Amazon SQS as the point-request queue
    /// (see <see cref="PointQueueConfiguration"/>).
    ///
    /// Credentials come from the default AWS chain — on EKS that is IRSA / Pod Identity,
    /// so no keys are stored in the cluster.
    /// </summary>
    public class SqsConfiguration
    {
        public const string SectionName = "Sqs";

        /// <summary>AWS region, e.g. "eu-central-1".</summary>
        public string Region { get; set; }

        /// <summary>URL of the queue the CPU daemon ScaledJob consumes.</summary>
        public string QueueUrl { get; set; }

        /// <summary>
        /// URL of the queue for jobs with RequiresGpu = true. Publisher side only; GPU daemon
        /// pods are configured with this URL as their QueueUrl.
        /// </summary>
        public string GpuQueueUrl { get; set; }

        /// <summary>Optional endpoint override, e.g. a LocalStack URL for local testing.</summary>
        public string ServiceUrl { get; set; }

        /// <summary>
        /// Visibility timeout applied on receive and renewed while the point is running, so the
        /// message is not redelivered to another pod in the middle of a long computation.
        /// </summary>
        public int VisibilityTimeoutSeconds { get; set; } = 120;
    }
}
