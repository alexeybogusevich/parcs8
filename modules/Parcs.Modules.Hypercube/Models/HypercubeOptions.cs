using Parcs.Net;

namespace Parcs.Modules.Hypercube.Models
{
    public class HypercubeOptions : IModuleOptions
    {
        /// <summary>Dimension d of the hypercube: the job runs 2^d points.</summary>
        public int Dimension { get; set; } = 3;

        /// <summary>Random integers each point generates and sorts.</summary>
        public int ElementsPerNode { get; set; } = 1_000_000;

        public int Seed { get; set; } = 42;
    }
}
