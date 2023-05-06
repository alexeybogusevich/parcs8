namespace Parcs.HostAPI.Configuration
{
    public sealed class JobTrackingConfiguration
    {
        public const string SectionName = "JobTracking";

        public int MaximumActiveJobs { get; set; }
    }
}