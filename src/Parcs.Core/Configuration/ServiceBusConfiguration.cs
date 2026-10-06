namespace Parcs.Core.Configuration
{
    /// <summary>
    /// Configuration for Azure Service Bus as the point-request queue
    /// (see <see cref="PointQueueConfiguration"/>).
    /// </summary>
    public class ServiceBusConfiguration
    {
        public const string SectionName = "ServiceBus";

        public string ConnectionString { get; set; }

        /// <summary>Queue the CPU daemon ScaledJob consumes, e.g. "point-requested".</summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Queue for jobs with RequiresGpu = true, consumed by the GPU daemon ScaledJob.
        /// Publisher side only; GPU daemon pods are configured with this name as their QueueName.
        /// </summary>
        public string GpuQueueName { get; set; }
    }
}
