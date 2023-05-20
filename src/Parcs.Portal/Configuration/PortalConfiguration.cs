namespace Parcs.Portal.Configuration
{
    public class PortalConfiguration
    {
        public const string SectionName = "Portal";

        public string HostName { get; set; }

        public string JobCompletionEndpoint { get; set; }
    }
}