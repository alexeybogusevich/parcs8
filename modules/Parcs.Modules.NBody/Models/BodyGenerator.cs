namespace Parcs.Modules.NBody.Models
{
    /// <summary>
    /// Deterministic initial conditions shared by both the CPU (Parallel) and GPU Main modules.
    /// Both call this with the same Seed, so a CPU run and a GPU run start from numerically
    /// identical bodies — required for the final-energy cross-check to mean anything, and for
    /// a fair head-to-head timing comparison in general.
    ///
    /// Bodies are placed uniformly in a cube and given a small random velocity; masses are drawn
    /// from a narrow range so no single body dominates the simulation. This is a synthetic
    /// benchmark distribution, not a physically realistic galaxy/cluster model — sufficient for
    /// a scaling/speedup study, not for astrophysical accuracy.
    /// </summary>
    public static class BodyGenerator
    {
        public static BodyChunk Generate(int totalBodies, int seed, out float[] masses)
        {
            var rng = new Random(seed);

            var x = new float[totalBodies];
            var y = new float[totalBodies];
            var z = new float[totalBodies];
            var vx = new float[totalBodies];
            var vy = new float[totalBodies];
            var vz = new float[totalBodies];
            masses = new float[totalBodies];

            const float boxSize = 100f;
            const float maxInitialSpeed = 0.5f;
            const float minMass = 0.5f;
            const float maxMass = 1.5f;

            for (int i = 0; i < totalBodies; i++)
            {
                x[i] = ((float)rng.NextDouble() - 0.5f) * boxSize;
                y[i] = ((float)rng.NextDouble() - 0.5f) * boxSize;
                z[i] = ((float)rng.NextDouble() - 0.5f) * boxSize;

                vx[i] = ((float)rng.NextDouble() - 0.5f) * 2f * maxInitialSpeed;
                vy[i] = ((float)rng.NextDouble() - 0.5f) * 2f * maxInitialSpeed;
                vz[i] = ((float)rng.NextDouble() - 0.5f) * 2f * maxInitialSpeed;

                masses[i] = minMass + (float)rng.NextDouble() * (maxMass - minMass);
            }

            return new BodyChunk { X = x, Y = y, Z = z, VX = vx, VY = vy, VZ = vz };
        }
    }
}
