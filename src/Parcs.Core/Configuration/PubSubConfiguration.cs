namespace Parcs.Core.Configuration
{
    /// <summary>
    /// Configuration for Google Cloud Pub/Sub messaging.
    /// GCP equivalent of <see cref="ServiceBusConfiguration"/> (Azure Service Bus).
    ///
    /// Mapping:
    ///   Azure Service Bus queue  →  Pub/Sub topic  +  subscription
    ///   ConnectionString         →  ProjectId (auth handled by Workload Identity / ADC)
    ///   QueueName                →  TopicId (for the publisher) / SubscriptionId (for the subscriber)
    /// </summary>
    public class PubSubConfiguration
    {
        public const string SectionName = "PubSub";

        /// <summary>GCP project ID, e.g. "my-parcs-project".</summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Pub/Sub topic ID that the Host publishes point-creation requests to,
        /// e.g. "point-requested".
        /// </summary>
        public string TopicId { get; set; }

        /// <summary>
        /// Pub/Sub subscription ID that daemon pods pull from (and KEDA monitors),
        /// e.g. "point-requested-sub".
        /// </summary>
        public string SubscriptionId { get; set; }
    }
}
