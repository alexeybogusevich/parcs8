namespace Parcs.HostAPI.Configuration
{
    public class InMemoryModulesConfiguration
    {
        public const string SectionName = "FileSystem";
        
        public int MaximumNumberOfSimultaneouslyStoredModules { get; set; }
    }
}