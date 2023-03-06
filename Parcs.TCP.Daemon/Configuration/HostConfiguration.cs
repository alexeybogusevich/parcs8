namespace Parcs.TCP.Daemon.Configuration
{
    public class HostConfiguration
    {
        public const string SectionName = "Host";

        public string IpAddress { get; set; }

        public int Port { get; set; }
    }
}