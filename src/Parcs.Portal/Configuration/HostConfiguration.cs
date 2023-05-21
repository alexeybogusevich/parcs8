namespace Parcs.Portal.Configuration
{
    public class HostConfiguration
    {
        public const string SectionName = "Portal";

        public string HostName { get; set; }

        public string GetModuleEndpoint { get; set; }

        public string GetModulesEndpoint { get; set; }

        public string PostModulesEndpoint { get; set; }

        public string DeleteModulesEndpoint { get; set; }

        public string GetJobEndpoint { get; set; }

        public string GetJobsEndpoint { get; set; }

        public string PostJobsEndpoint { get; set; }

        public string PostAsynchronousRunsEndpoint { get; set; }
    }
}