namespace Parcs.Daemon.Configuration
{
    public class HostApiConfiguration
    {
        public const string SectionName = "HostApi";

        public string Uri { get; set; }

        public string JobFailuresPath { get; set; }
    }
}