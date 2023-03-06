namespace Parcs.TCP.Daemon.Configuration
{
    public class NodeConfiguration
    {
        public const string SectionName = "Node";

        public string IpAddress { get; set; }

        public int Port { get; set; }
    }
}