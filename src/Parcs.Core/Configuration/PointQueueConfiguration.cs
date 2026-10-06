namespace Parcs.Core.Configuration
{
    /// <summary>
    /// Selects the message broker that carries point-creation requests from a point's parent
    /// (the Host or another daemon) to the KEDA-scaled daemon pool.
    ///
    /// Each cloud has a native broker with a matching KEDA scaler:
    ///   Azure  →  Service Bus queue      (KEDA "azure-servicebus")
    ///   GCP    →  Pub/Sub subscription   (KEDA "gcp-pubsub")
    ///   AWS    →  SQS queue              (KEDA "aws-sqs-queue")
    ///
    /// When <see cref="Provider"/> is empty it is inferred from whichever provider section is
    /// filled in; when none is, point creation falls back to direct TCP to pre-provisioned
    /// daemons (local Docker Compose / static Kubernetes deployments).
    /// </summary>
    public class PointQueueConfiguration
    {
        public const string SectionName = "PointQueue";

        /// <summary>One of <see cref="PointQueueProviders"/>; empty to infer.</summary>
        public string Provider { get; set; }
    }

    public static class PointQueueProviders
    {
        public const string None = "None";
        public const string PubSub = "PubSub";
        public const string ServiceBus = "ServiceBus";
        public const string Sqs = "Sqs";
    }
}
