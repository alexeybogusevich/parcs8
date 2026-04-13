using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Gpu
{
    /// <summary>
    /// GPU-accelerated worker module for matrix multiplication.
    ///
    /// Receives two sub-matrices from the main module, multiplies them on the GPU
    /// using an ILGPU CUDA kernel, and sends the result back.
    ///
    /// The kernel maps one thread per output element (i, j) and accumulates the
    /// dot product over the shared inner dimension k.  For large matrices this is
    /// orders-of-magnitude faster than the CPU triple-nested loop because thousands
    /// of elements are computed in parallel across GPU warps.
    ///
    /// Fallback: if no CUDA device is available (e.g. local development without a GPU),
    /// ILGPU automatically selects the CPU accelerator so the module still works correctly.
    /// </summary>
    public class GpuWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            matrixA.GpuMultiplyBy(matrixB);

            await moduleInfo.Parent.WriteObjectAsync(matrixA);
        }
    }

    /// <summary>
    /// Extension that adds GPU multiplication to the existing <see cref="Matrix"/> type
    /// without modifying the original class (open/closed principle).
    /// </summary>
    internal static class MatrixGpuExtensions
    {
        // Static context and accelerator are reused across calls within the same process
        // to avoid the overhead of CUDA context initialisation on every invocation.
        private static readonly Lazy<(Context ctx, Accelerator acc)> _accelerator =
            new(CreateAccelerator, LazyThreadSafetyMode.ExecutionAndPublication);

        private static (Context ctx, Accelerator acc) CreateAccelerator()
        {
            var context = Context.CreateDefault();

            // Prefer CUDA; fall back to the CPU accelerator when no NVIDIA GPU is present.
            Accelerator acc;
            if (context.GetCudaDevices().Count > 0)
            {
                acc = context.CreateCudaAccelerator(0);
            }
            else
            {
                acc = context.CreateCPUAccelerator(0);
            }

            return (context, acc);
        }

        /// <summary>
        /// Multiplies <paramref name="self"/> by <paramref name="other"/> in-place using the GPU.
        /// The result is stored back into <paramref name="self"/> (same semantics as the CPU version).
        /// </summary>
        internal static void GpuMultiplyBy(this Matrix self, Matrix other)
        {
            if (self.Width != other.Height)
            {
                throw new ArgumentException("Cannot multiply matrices with incompatible dimensions.");
            }

            int m = self.Height;
            int n = other.Width;
            int k = self.Width;

            var (_, acc) = _accelerator.Value;

            // Flatten the jagged List<List<int>> into 1-D int[] for GPU transfer.
            var flatA = Flatten(self);
            var flatB = Flatten(other);
            var flatC = new int[m * n];

            using var bufA = acc.Allocate1D(flatA);
            using var bufB = acc.Allocate1D(flatB);
            using var bufC = acc.Allocate1D<int>(m * n);

            // Load the kernel. ILGPU JIT-compiles it to PTX on first call per accelerator type.
            var kernel = acc.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView1D<int, Stride1D.Dense>,
                ArrayView1D<int, Stride1D.Dense>,
                ArrayView1D<int, Stride1D.Dense>,
                int, int, int>(MatMulKernel);

            // Launch one thread per output element.
            kernel((int)(m * n), bufA.View, bufB.View, bufC.View, m, n, k);
            acc.Synchronize();

            bufC.CopyToCPU(flatC);

            // Write results back into the Matrix's jagged structure.
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    self[i, j] = flatC[i * n + j];
                }
            }

            // Resize Width if other.Width != self.Width (non-square case).
            self.Width = n;
        }

        /// <summary>
        /// ILGPU kernel — runs on the GPU.  Each thread computes one element of C = A × B.
        /// Row-major layout: element [i, j] is at index i*cols + j.
        /// </summary>
        static void MatMulKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> a,
            ArrayView1D<int, Stride1D.Dense> b,
            ArrayView1D<int, Stride1D.Dense> c,
            int m,
            int n,
            int k)
        {
            int idx = index;
            if (idx >= m * n) return;

            int row = idx / n;
            int col = idx % n;

            int sum = 0;
            for (int i = 0; i < k; i++)
            {
                sum += a[row * k + i] * b[i * n + col];
            }

            c[idx] = sum;
        }

        private static int[] Flatten(Matrix m)
        {
            var flat = new int[m.Height * m.Width];
            for (int i = 0; i < m.Height; i++)
            {
                for (int j = 0; j < m.Width; j++)
                {
                    flat[i * m.Width + j] = m[i, j];
                }
            }
            return flat;
        }
    }
}
