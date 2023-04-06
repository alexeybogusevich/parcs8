using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Configuration
{
    public class HostingConfiguration
    {
        public const string SectionName = "Hosting";

        public HostingEnvironment Environment { get; set; }
    }
}