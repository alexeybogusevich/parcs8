namespace Parcs.Daemon.Configuration
{
    public class ApplicationInsightsConfiguration
    {
        public const string SectionName = "ApplicationInsights";

        public string ConnectionString { get; set; }
    }
}