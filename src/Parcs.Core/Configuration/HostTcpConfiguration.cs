namespace Parcs.Core.Configuration
{
    public class HostTcpConfiguration
    {
        public const string SectionName = "HostTcp";

        public int Port { get; set; } = 2222;

        /// <summary>
        /// How long to wait for every requested daemon to connect back before failing point
        /// creation. Without this, a lost Pub/Sub message or a daemon pod that never schedules
        /// leaves the caller waiting forever, since nothing else in the point-creation path
        /// times out.
        ///
        /// KEDA's gcp-pubsub trigger scales purely off the Cloud Monitoring
        /// "num_undelivered_messages" metric, which has been observed lagging real publish time
        /// by up to ~4 minutes (measured directly: messages published at T+0 didn't show up in
        /// the metric until T+4m, so KEDA had nothing to scale on until then). This default must
        /// comfortably clear that lag plus KEDA's own polling interval and daemon pod cold start,
        /// or every layer on a quiet subscription false-positives as "daemon didn't connect".
        /// </summary>
        public int DaemonConnectTimeoutSeconds { get; set; } = 420;
    }
}
