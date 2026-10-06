using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using Parcs.Modules.NBody.Models;
using Parcs.Net;

namespace Parcs.Modules.NBody.Gpu
{
    /// <summary>
    /// GPU-accelerated worker: one CUDA thread per body in this worker's chunk. Each thread
    /// loops over all TotalBodies bodies (the broadcast position snapshot, uploaded fresh every
    /// step since positions change globally each step) to accumulate gravitational acceleration,
    /// then integrates its body one step forward — the same per-body work as the CPU worker,
    /// just data-parallel across chunkSize threads instead of a sequential loop.
    ///
    /// Uses Context.Builder.EnableAlgorithms() (unlike the other GPU modules in this repo) because
    /// this is the first one that needs an XMath intrinsic (Rsqrt) inside the kernel — ILGPU
    /// requires algorithms support to be explicitly enabled for that to JIT-compile on CUDA.
    ///
    /// Fallback: ILGPU automatically uses the CPU accelerator when no CUDA device is found, so
    /// this module is still functionally correct (just not faster) without a GPU.
    /// </summary>
    public class NBodyGpuWorkerModule : IModule
    {
        private static readonly Lazy<(Context ctx, Accelerator acc)> _accelerator =
            new(CreateAccelerator, LazyThreadSafetyMode.ExecutionAndPublication);

        private static (Context ctx, Accelerator acc) CreateAccelerator()
        {
            var context = Context.Create(builder => builder.Default().EnableAlgorithms());
            Accelerator acc = context.GetCudaDevices().Count > 0
                ? context.CreateCudaAccelerator(0)
                : context.CreateCPUAccelerator(0);
            return (context, acc);
        }

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

            var (_, acc) = _accelerator.Value;

            using var gpuOwnX = acc.Allocate1D(own.X);
            using var gpuOwnY = acc.Allocate1D(own.Y);
            using var gpuOwnZ = acc.Allocate1D(own.Z);
            using var gpuOwnVX = acc.Allocate1D(own.VX);
            using var gpuOwnVY = acc.Allocate1D(own.VY);
            using var gpuOwnVZ = acc.Allocate1D(own.VZ);
            using var gpuMasses = acc.Allocate1D(masses);

            // Full-snapshot buffers are re-filled every step (positions change globally each
            // step); allocated once here and reused via CopyFromCPU to avoid re-allocating VRAM
            // every iteration.
            using var gpuSnapX = acc.Allocate1D<float>(totalBodies);
            using var gpuSnapY = acc.Allocate1D<float>(totalBodies);
            using var gpuSnapZ = acc.Allocate1D<float>(totalBodies);

            var kernel = acc.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>,
                ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>,
                ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>,
                ArrayView1D<float, Stride1D.Dense>,
                int, int, float, float>(NBodyStepKernel);

            for (int step = 0; step < steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                gpuSnapX.CopyFromCPU(snapshot.X);
                gpuSnapY.CopyFromCPU(snapshot.Y);
                gpuSnapZ.CopyFromCPU(snapshot.Z);

                kernel(
                    chunkSize,
                    gpuOwnX.View, gpuOwnY.View, gpuOwnZ.View,
                    gpuOwnVX.View, gpuOwnVY.View, gpuOwnVZ.View,
                    gpuSnapX.View, gpuSnapY.View, gpuSnapZ.View,
                    gpuMasses.View,
                    start, totalBodies, dt, softeningSquared);
                acc.Synchronize();

                gpuOwnX.CopyToCPU(own.X);
                gpuOwnY.CopyToCPU(own.Y);
                gpuOwnZ.CopyToCPU(own.Z);

                await channel.WriteObjectAsync(new PositionSnapshot { X = own.X, Y = own.Y, Z = own.Z });

                var isLastStep = step == steps - 1;
                if (!isLastStep)
                {
                    snapshot = await channel.ReadObjectAsync<PositionSnapshot>();
                }
            }

            var finalVX = gpuOwnVX.GetAsArray1D();
            var finalVY = gpuOwnVY.GetAsArray1D();
            var finalVZ = gpuOwnVZ.GetAsArray1D();

            var finalVelocitySquared = new float[chunkSize];
            for (int i = 0; i < chunkSize; i++)
            {
                finalVelocitySquared[i] = finalVX[i] * finalVX[i] + finalVY[i] * finalVY[i] + finalVZ[i] * finalVZ[i];
            }

            await channel.WriteObjectAsync(finalVelocitySquared);
        }

        /// <summary>
        /// ILGPU kernel — runs on GPU. One thread per local body index i (0..chunkSize-1).
        /// Computes net gravitational acceleration on body (start+i) from all totalBodies bodies
        /// in the broadcast snapshot, then integrates velocity and position one step
        /// (semi-implicit/symplectic Euler) — identical math to the CPU worker.
        /// </summary>
        static void NBodyStepKernel(
            Index1D i,
            ArrayView1D<float, Stride1D.Dense> ownX, ArrayView1D<float, Stride1D.Dense> ownY, ArrayView1D<float, Stride1D.Dense> ownZ,
            ArrayView1D<float, Stride1D.Dense> ownVX, ArrayView1D<float, Stride1D.Dense> ownVY, ArrayView1D<float, Stride1D.Dense> ownVZ,
            ArrayView1D<float, Stride1D.Dense> snapX, ArrayView1D<float, Stride1D.Dense> snapY, ArrayView1D<float, Stride1D.Dense> snapZ,
            ArrayView1D<float, Stride1D.Dense> masses,
            int start, int totalBodies, float dt, float softeningSquared)
        {
            int globalIndex = start + i;
            float px = ownX[i];
            float py = ownY[i];
            float pz = ownZ[i];

            float ax = 0f, ay = 0f, az = 0f;

            for (int j = 0; j < totalBodies; j++)
            {
                if (j == globalIndex)
                {
                    continue;
                }

                float dx = snapX[j] - px;
                float dy = snapY[j] - py;
                float dz = snapZ[j] - pz;
                float distSquared = dx * dx + dy * dy + dz * dz + softeningSquared;
                float invDist = XMath.Rsqrt(distSquared);
                float invDist3 = invDist * invDist * invDist;
                float f = masses[j] * invDist3;

                ax += f * dx;
                ay += f * dy;
                az += f * dz;
            }

            float vx = ownVX[i] + ax * dt;
            float vy = ownVY[i] + ay * dt;
            float vz = ownVZ[i] + az * dt;

            ownVX[i] = vx;
            ownVY[i] = vy;
            ownVZ[i] = vz;

            ownX[i] = px + vx * dt;
            ownY[i] = py + vy * dt;
            ownZ[i] = pz + vz * dt;
        }
    }
}
