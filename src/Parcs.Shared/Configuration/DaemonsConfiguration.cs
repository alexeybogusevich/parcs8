using Parcs.Shared.Models;

namespace Parcs.Shared.Configuration
{
    public sealed class DaemonsConfiguration
    {
        public const string SectionName = "Daemons";

        public IEnumerable<Daemon> PreconfiguredInstances { get; set; }
    }
}