using Parcs.Net;

namespace Parcs.Modules.NBody
{
    /// <summary>
    /// TotalBodies must be evenly divisible by PointsNumber (same convention as FloydWarshall's
    /// VerticesNumber/PointsNumber) — each worker owns an equal, contiguous chunk of bodies.
    /// </summary>
    public class ModuleOptions : IModuleOptions
    {
        public int TotalBodies { get; set; } = 1024;

        public int PointsNumber { get; set; } = 4;

        public int Steps { get; set; } = 20;

        public float Dt { get; set; } = 0.01f;

        /// <summary>
        /// Softening length (Plummer softening) added to the squared distance before the inverse-
        /// square law is applied, to avoid a singularity when two bodies nearly coincide.
        /// </summary>
        public float Softening { get; set; } = 0.1f;

        public int Seed { get; set; } = 42;

        public string OutputFile { get; set; } = "Output.json";
    }
}
