using Parcs.Core.Models;

namespace Parcs.Core.Configuration
{
    public sealed class DaemonsConfiguration
    {
        public const string SectionName = "Daemons";

        public IEnumerable<Daemon> PreconfiguredInstances { get; set; }
    }
}