namespace Parcs.TCP.Daemon.Configuration
{
    public sealed class NodeConfiguration
    {
        public const string SectionName = "Node";

        public int Port { get; set; }
    }
}