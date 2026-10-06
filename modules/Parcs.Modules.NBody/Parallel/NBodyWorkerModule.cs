using Parcs.Modules.NBody.Models;
using Parcs.Net;

namespace Parcs.Modules.NBody.Parallel
{
    /// <summary>
    /// CPU baseline worker: for each of its chunkSize bodies, computes the net gravitational
    /// acceleration from all TotalBodies bodies (O(chunkSize * TotalBodies) per step, plain
    /// sequential nested loop — see CLAUDE.md convention: "Parallel" here means distributed
    /// across PARCS workers, not multi-threaded within a worker, matching the other modules'
    /// Parallel variants), then integrates velocity and position one step forward
    /// (symplectic/semi-implicit Euler).
    /// </summary>
    public class NBodyWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var channel = moduleInfo.Parent;

            var workerIndex = await channel.ReadIntAsync();
            var chunkSize = await channel.ReadIntAsync();
            var totalBodies = await channel.ReadIntAsync();
            var steps = await channel.ReadIntAsync();
            var dt = await channel.ReadObjectAsync<float>();
            var softening = await channel.ReadObjectAsync<float>();

            var own = await channel.ReadObjectAsync<BodyChunk>();
            var snapshot = await channel.ReadObjectAsync<PositionSnapshot>();
            var masses = await channel.ReadObjectAsync<float[]>();

            var start = workerIndex * chunkSize;
            var softeningSquared = softening * softening;

            for (int step = 0; step < steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < chunkSize; i++)
                {
                    var globalIndex = start + i;
                    float ax = 0, ay = 0, az = 0;

                    for (int j = 0; j < totalBodies; j++)
                    {
                        if (j == globalIndex)
                        {
                            continue;
                        }

                        var dx = snapshot.X[j] - own.X[i];
                        var dy = snapshot.Y[j] - own.Y[i];
                        var dz = snapshot.Z[j] - own.Z[i];
                        var distSquared = dx * dx + dy * dy + dz * dz + softeningSquared;
                        var invDist = 1f / MathF.Sqrt(distSquared);
                        var invDist3 = invDist * invDist * invDist;
                        var f = masses[j] * invDist3;

                        ax += f * dx;
                        ay += f * dy;
                        az += f * dz;
                    }

                    own.VX[i] += ax * dt;
                    own.VY[i] += ay * dt;
                    own.VZ[i] += az * dt;

                    own.X[i] += own.VX[i] * dt;
                    own.Y[i] += own.VY[i] * dt;
                    own.Z[i] += own.VZ[i] * dt;
                }

                await channel.WriteObjectAsync(new PositionSnapshot { X = own.X, Y = own.Y, Z = own.Z });

                var isLastStep = step == steps - 1;
                if (!isLastStep)
                {
                    snapshot = await channel.ReadObjectAsync<PositionSnapshot>();
                }
            }

            var finalVelocitySquared = new float[chunkSize];
            for (int i = 0; i < chunkSize; i++)
            {
                finalVelocitySquared[i] = own.VX[i] * own.VX[i] + own.VY[i] * own.VY[i] + own.VZ[i] * own.VZ[i];
            }

            await channel.WriteObjectAsync(finalVelocitySquared);
        }
    }
}
