using System.Diagnostics;
using System.Text.Json;
using Parcs.Modules.NBody;
using Parcs.Modules.NBody.Models;
using Parcs.Net;

namespace Parcs.Modules.NBody.Parallel
{
    /// <summary>
    /// CPU baseline for the N-body gravitational simulation benchmark.
    ///
    /// Protocol (mirrors FloydWarshall's gather/broadcast-per-pivot style, generalised to every
    /// body needing every other body's position each step rather than just one pivot row):
    ///   1. Generate TotalBodies bodies once (BodyGenerator, seeded — identical across the CPU
    ///      and GPU variants for a fair, reproducible comparison).
    ///   2. Each of PointsNumber workers gets: its own chunk's initial positions/velocities
    ///      (which it owns and mutates), the full initial position snapshot, and the full
    ///      (static) mass array.
    ///   3. For each of Steps iterations: every worker integrates its own chunk forward one
    ///      step using the current full snapshot, then sends its updated chunk's positions back
    ///      (gather); the Main module reassembles the full snapshot and — on every step but the
    ///      last — broadcasts it back out so the next step uses up-to-date positions.
    ///   4. After the loop, Main also reads back each worker's final chunk velocities (one
    ///      extra small round-trip) to report total kinetic/potential energy as a correctness
    ///      cross-check against the GPU variant's output for the same Seed.
    /// </summary>
    public class NBodyMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<ModuleOptions>();

            if (options.TotalBodies % options.PointsNumber != 0)
            {
                throw new ArgumentException(
                    $"TotalBodies ({options.TotalBodies}) must be divisible by PointsNumber ({options.PointsNumber})");
            }

            var chunkSize = options.TotalBodies / options.PointsNumber;
            var initial = BodyGenerator.Generate(options.TotalBodies, options.Seed, out var masses);

            var points = new IPoint[options.PointsNumber];
            var channels = new IChannel[options.PointsNumber];

            for (int w = 0; w < options.PointsNumber; w++)
            {
                points[w] = await moduleInfo.CreatePointAsync();
                channels[w] = await points[w].CreateChannelAsync();
                await points[w].ExecuteClassAsync<NBodyWorkerModule>();

                var start = w * chunkSize;

                await channels[w].WriteDataAsync(w);
                await channels[w].WriteDataAsync(chunkSize);
                await channels[w].WriteDataAsync(options.TotalBodies);
                await channels[w].WriteDataAsync(options.Steps);
                await channels[w].WriteObjectAsync(options.Dt);
                await channels[w].WriteObjectAsync(options.Softening);

                await channels[w].WriteObjectAsync(new BodyChunk
                {
                    X = initial.X[start..(start + chunkSize)],
                    Y = initial.Y[start..(start + chunkSize)],
                    Z = initial.Z[start..(start + chunkSize)],
                    VX = initial.VX[start..(start + chunkSize)],
                    VY = initial.VY[start..(start + chunkSize)],
                    VZ = initial.VZ[start..(start + chunkSize)],
                });

                await channels[w].WriteObjectAsync(new PositionSnapshot { X = initial.X, Y = initial.Y, Z = initial.Z });
                await channels[w].WriteObjectAsync(masses);
            }

            var stopwatch = Stopwatch.StartNew();

            var fullX = initial.X;
            var fullY = initial.Y;
            var fullZ = initial.Z;

            for (int step = 0; step < options.Steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isLastStep = step == options.Steps - 1;

                for (int w = 0; w < options.PointsNumber; w++)
                {
                    var chunk = await channels[w].ReadObjectAsync<PositionSnapshot>();
                    var start = w * chunkSize;
                    Array.Copy(chunk.X, 0, fullX, start, chunkSize);
                    Array.Copy(chunk.Y, 0, fullY, start, chunkSize);
                    Array.Copy(chunk.Z, 0, fullZ, start, chunkSize);
                }

                if (!isLastStep)
                {
                    var snapshot = new PositionSnapshot { X = fullX, Y = fullY, Z = fullZ };
                    for (int w = 0; w < options.PointsNumber; w++)
                    {
                        await channels[w].WriteObjectAsync(snapshot);
                    }
                }
            }

            double kineticEnergy = 0;
            for (int w = 0; w < options.PointsNumber; w++)
            {
                // Worker sends v^2 = vx^2+vy^2+vz^2 per body; KE = 0.5 * m * v^2.
                var velocitySquared = await channels[w].ReadObjectAsync<float[]>();
                var start = w * chunkSize;
                for (int i = 0; i < chunkSize; i++)
                {
                    kineticEnergy += 0.5 * masses[start + i] * velocitySquared[i];
                }
            }

            stopwatch.Stop();

            double potentialEnergy = ComputePotentialEnergy(fullX, fullY, fullZ, masses, options.Softening);

            var output = new ModuleOutput
            {
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                TotalBodies = options.TotalBodies,
                Steps = options.Steps,
                KineticEnergy = kineticEnergy,
                PotentialEnergy = potentialEnergy,
                TotalEnergy = kineticEnergy + potentialEnergy,
            };

            await moduleInfo.OutputWriter.WriteToFileAsync(
                JsonSerializer.SerializeToUtf8Bytes(output), options.OutputFile);

            foreach (var point in points)
            {
                try { await point.DeleteAsync(); } catch { }
            }

            foreach (var channel in channels)
            {
                try { channel.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// U = -G * sum over all pairs i&lt;j of (m_i * m_j) / sqrt(r^2 + softening^2). G is taken
        /// as 1 (standard for synthetic N-body benchmarks that aren't modelling real units).
        /// </summary>
        internal static double ComputePotentialEnergy(float[] x, float[] y, float[] z, float[] masses, float softening)
        {
            double potential = 0;
            var n = x.Length;
            var softeningSquared = (double)softening * softening;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var dx = (double)x[i] - x[j];
                    var dy = (double)y[i] - y[j];
                    var dz = (double)z[i] - z[j];
                    var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz + softeningSquared);
                    potential -= masses[i] * masses[j] / distance;
                }
            }

            return potential;
        }
    }
}
