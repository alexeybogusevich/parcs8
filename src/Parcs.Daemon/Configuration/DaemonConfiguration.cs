namespace Parcs.Daemon.Configuration
{
    public sealed class DaemonConfiguration
    {
        public const string SectionName = "Node";

        public int Port { get; set; }
    }
}