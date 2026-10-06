namespace Parcs.Modules.NBody
{
    /// <summary>
    /// KineticEnergy/PotentialEnergy/TotalEnergy are a correctness cross-check, not just
    /// diagnostics: for the same initial conditions (Seed), the CPU and GPU variants should
    /// report closely matching final energy — a large divergence indicates a bug in one of the
    /// two force/integration implementations, independent of the timing comparison.
    /// </summary>
    public class ModuleOutput
    {
        public double ElapsedSeconds { get; set; }

        public int TotalBodies { get; set; }

        public int Steps { get; set; }

        public double KineticEnergy { get; set; }

        public double PotentialEnergy { get; set; }

        public double TotalEnergy { get; set; }
    }
}
