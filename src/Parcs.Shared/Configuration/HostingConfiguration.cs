using Parcs.Shared.Models.Enums;

namespace Parcs.Shared.Configuration
{
    public class HostingConfiguration
    {
        public const string SectionName = "Hosting";

        public HostingEnvironment Environment { get; set; }
    }
}