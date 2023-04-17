using Parcs.Core.Models.Enums;

namespace Parcs.Core.Configuration
{
    public class HostingConfiguration
    {
        public const string SectionName = "Hosting";

        public HostingEnvironment Environment { get; set; }
    }
}