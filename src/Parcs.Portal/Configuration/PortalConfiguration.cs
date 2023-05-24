namespace Parcs.Portal.Configuration
{
    public class PortalConfiguration
    {
        public const string SectionName = "Portal";

        public string Uri { get; set; }

        public string SignalrUri { get; set; }

        public string JobCompletionEndpoint { get; set; }
    }
}