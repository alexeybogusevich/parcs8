namespace Parcs.Daemon.Configuration
{
    public class HostConfiguration
    {
        public const string SectionName = "HostApi";

        public string Uri { get; set; }

        public string JobFailuresPath { get; set; }
    }
}