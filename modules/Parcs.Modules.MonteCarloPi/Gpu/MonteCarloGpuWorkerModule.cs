using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Microsoft.Extensions.Logging;
using Parcs.Net;

namespace Parcs.Modules.MonteCarloPi.Gpu
{
    /// <summary>
    /// GPU-accelerated worker module for Monte Carlo π estimation.
    ///
    /// Monte Carlo sampling is embarrassingly parallel — each sample is independent —
    /// making it a textbook GPU workload.  This module generates millions of random
    /// points on the GPU in parallel and counts how many fall inside the unit circle.
    ///
    /// Random number generation uses a simple LCG (Linear Congruential Generator)
    /// seeded per-thread.  LCG is not cryptographically secure but is fast and
    /// sufficient for statistical sampling.  Each thread uses a unique seed derived
    /// from its global thread index and the seed received from the main module.
    ///
    /// Fallback: ILGPU automatically uses the CPU accelerator when no CUDA device is
    /// found, so the module works correctly in local development without a GPU.
    /// </summary>
    public class MonteCarloGpuWorkerModule : IModule
    {
        private static readonly Lazy<(Context ctx, Accelerator acc)> _accelerator =
            new(CreateAccelerator, LazyThreadSafetyMode.ExecutionAndPublication);

        private static (Context ctx, Accelerator acc) CreateAccelerator()
        {
            var context = Context.CreateDefault();
            Accelerator acc = context.GetCudaDevices().Count > 0
                ? context.CreateCudaAccelerator(0)
                : context.CreateCPUAccelerator(0);
            return (context, acc);
        }

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("Monte Carlo GPU Worker started");

            var samples = await moduleInfo.Parent.ReadDataAsync<long>();
            var seed = await moduleInfo.Parent.ReadDataAsync<int>();

            moduleInfo.Logger.LogInformation(
                "GPU Worker processing {Samples:N0} samples with seed {Seed}", samples, seed);

            long hits = RunGpuMonteCarlo(samples, seed);

            moduleInfo.Logger.LogInformation(
                "GPU Worker completed: {Hits:N0} hits out of {Samples:N0} samples", hits, samples);

            await moduleInfo.Parent.WriteDataAsync(hits);
        }

        private static long RunGpuMonteCarlo(long totalSamples, int baseSeed)
        {
            var (_, acc) = _accelerator.Value;

            // Partition work across threads.  Each thread handles a contiguous block of samples.
            // Use the GPU's warp size (32) as the granularity; target ~64k threads for good occupancy.
            int numThreads = (int)Math.Min(totalSamples, 65536);
            long samplesPerThread = totalSamples / numThreads;
            long remainder = totalSamples % numThreads;

            // Output buffer: one hit-count per thread.
            using var gpuHits = acc.Allocate1D<long>(numThreads);
            using var gpuSamplesPerThread = acc.Allocate1D(new long[] { samplesPerThread });
            using var gpuRemainder = acc.Allocate1D(new long[] { remainder });

            var kernel = acc.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView1D<long, Stride1D.Dense>,
                long, long, int>(MonteCarloKernel);

            kernel(numThreads, gpuHits.View, samplesPerThread, remainder, baseSeed);
            acc.Synchronize();

            var hostHits = gpuHits.GetAsArray1D();
            return hostHits.Sum();
        }

        /// <summary>
        /// ILGPU kernel — runs on GPU.
        /// Each thread runs its own LCG-based RNG and counts hits independently.
        /// Thread 0 also handles the remainder samples so the total is exact.
        /// </summary>
        static void MonteCarloKernel(
            Index1D threadIdx,
            ArrayView1D<long, Stride1D.Dense> hits,
            long samplesPerThread,
            long remainder,
            int baseSeed)
        {
            int tid = threadIdx;

            // Per-thread LCG seed (Wang hash for better mixing than tid alone).
            uint state = WangHash((uint)(baseSeed ^ (tid * 1664525 + 1013904223)));

            long count = samplesPerThread;
            if (tid == 0) count += remainder; // thread 0 absorbs the remainder

            long localHits = 0;

            for (long s = 0; s < count; s++)
            {
                // Generate two independent floats in [0, 1).
                state = LcgNext(state);
                double x = (state & 0x7FFFFF) / (double)0x800000; // 23-bit mantissa

                state = LcgNext(state);
                double y = (state & 0x7FFFFF) / (double)0x800000;

                if (x * x + y * y <= 1.0)
                {
                    localHits++;
                }
            }

            hits[tid] = localHits;
        }

        // ── LCG random number generator (Numerical Recipes constants) ─────────────────────────

        static uint LcgNext(uint state) => state * 1664525u + 1013904223u;

        // ── Wang hash for seed mixing ─────────────────────────────────────────────────────────

        static uint WangHash(uint seed)
        {
            seed = (seed ^ 61u) ^ (seed >> 16);
            seed *= 9u;
            seed ^= seed >> 4;
            seed *= 0x27d4eb2du;
            seed ^= seed >> 15;
            return seed;
        }
    }
}
