namespace Parcs.HostAPI.Configuration
{
    public sealed class FileSystemConfiguration
    {
        public const string SectionName = "FileSystem";

        public string BasePath { get; set; }
    }
}