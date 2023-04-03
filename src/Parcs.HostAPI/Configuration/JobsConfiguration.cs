namespace Parcs.HostAPI.Configuration
{
    public sealed class JobsConfiguration
    {
        public const string SectionName = "Jobs";

        public int MaximumActiveJobs { get; set; }
    }
}