using System.Diagnostics;
using System.Text.Json;
using Parcs.Modules.NBody;
using Parcs.Modules.NBody.Models;
using Parcs.Modules.NBody.Parallel;
using Parcs.Net;

namespace Parcs.Modules.NBody.Gpu
{
    /// <summary>
    /// GPU-accelerated N-body simulation. Orchestration (gather/broadcast per step, final energy
    /// cross-check) is identical to <see cref="NBodyMainModule"/> (CPU variant); the only
    /// difference is dispatching <see cref="NBodyGpuWorkerModule"/> instead of
    /// <see cref="NBodyWorkerModule"/>, so each worker's per-step force computation runs on its
    /// daemon's GPU. Uses the same BodyGenerator/Seed as the CPU variant so results are directly
    /// comparable for the same input size.
    /// </summary>
    public class NBodyGpuMainModule : IModule
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
                await points[w].ExecuteClassAsync<NBodyGpuWorkerModule>();

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
                var velocitySquared = await channels[w].ReadObjectAsync<float[]>();
                var start = w * chunkSize;
                for (int i = 0; i < chunkSize; i++)
                {
                    kineticEnergy += 0.5 * masses[start + i] * velocitySquared[i];
                }
            }

            stopwatch.Stop();

            double potentialEnergy = NBodyMainModule.ComputePotentialEnergy(fullX, fullY, fullZ, masses, options.Softening);

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
    }
}
