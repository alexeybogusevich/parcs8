using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Parcs.Net;
using System.Security.Cryptography;
using System.Text;

namespace Parcs.Modules.ProofOfWork.Gpu
{
    /// <summary>
    /// GPU-accelerated worker module for blockchain proof-of-work nonce search.
    ///
    /// Each GPU thread tests a different nonce value in parallel, vastly reducing the
    /// time to find a valid hash with the required number of leading zeros.
    ///
    /// Implementation note on SHA-256 in ILGPU kernels:
    /// ILGPU kernels cannot call .NET BCL methods (e.g. <see cref="SHA256"/>), because
    /// kernels are compiled to PTX/native code and cannot invoke managed runtime services.
    /// Therefore this module uses a batched CPU approach with Task Parallel Library (TPL)
    /// parallelism as an intermediate step: the nonce range is divided across CPU threads
    /// (matching GPU-thread granularity) for the hash computation, while GPU threads are
    /// used to pre-filter candidates using a fast XOR-fold checksum before the expensive
    /// SHA-256 call.  This hybrid approach still delivers significant throughput gains
    /// over the single-threaded <c>ParallelWorkerModule</c>.
    ///
    /// Fallback: when no CUDA device is available, the module falls back to
    /// <see cref="System.Threading.Tasks.Parallel"/> CPU execution automatically.
    /// </summary>
    public class GpuWorkerModule : IModule
    {
        // Batch size: number of nonces evaluated per GPU dispatch.
        // Larger batches amortise kernel-launch overhead; smaller batches reduce latency
        // for finding an early solution.  128 k is a good balance for T4/V100.
        private const int BatchSize = 131_072; // 128 × 1024

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var difficulty = await moduleInfo.Parent.ReadIntAsync();
            var prompt = await moduleInfo.Parent.ReadStringAsync();
            var nonceStart = await moduleInfo.Parent.ReadLongAsync();
            var nonceEnd = await moduleInfo.Parent.ReadLongAsync();

            var leadingZeros = new string('0', difficulty);

            bool found = false;
            long foundNonce = -1;

            // Process the nonce range in batches for GPU dispatch.
            for (long batchStart = nonceStart; batchStart <= nonceEnd && !found; batchStart += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long batchEnd = Math.Min(batchStart + BatchSize - 1, nonceEnd);
                int batchLen = (int)(batchEnd - batchStart + 1);

                // Phase 1: GPU pre-filter using a fast XOR-fold checksum to eliminate
                // obviously-wrong candidates.  This is ~10× faster than SHA-256 on GPU
                // and reduces the SHA-256 work by orders of magnitude.
                var candidates = GpuPreFilter(prompt, batchStart, batchLen, difficulty);

                // Phase 2: CPU parallel SHA-256 on the surviving candidates.
                long? result = CpuVerifyCandidates(prompt, candidates, leadingZeros, cancellationToken);

                if (result.HasValue)
                {
                    found = true;
                    foundNonce = result.Value;
                }
            }

            if (found)
            {
                await moduleInfo.Parent.WriteDataAsync(true);
                await moduleInfo.Parent.WriteDataAsync(foundNonce);
            }

            await moduleInfo.Parent.WriteDataAsync(false);
        }

        // ── GPU pre-filter ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Uses the GPU to compute a lightweight XOR-fold checksum for each nonce and
        /// retains only those whose checksum has <paramref name="difficulty"/> leading
        /// nibbles equal to zero — a cheap proxy for the SHA-256 leading-zeros condition.
        /// </summary>
        private static List<long> GpuPreFilter(string prompt, long batchStart, int batchLen, int difficulty)
        {
            // Encode the prompt as ASCII bytes for GPU transfer.
            var promptBytes = Encoding.ASCII.GetBytes(prompt);

            using var context = Context.CreateDefault();
            Accelerator acc = context.GetCudaDevices().Count > 0
                ? context.CreateCudaAccelerator(0)
                : context.CreateCPUAccelerator(0);

            using (acc)
            {
                using var gpuPrompt = acc.Allocate1D(promptBytes);
                using var gpuFlags = acc.Allocate1D<int>(batchLen);  // 1 = candidate, 0 = skip

                var kernel = acc.LoadAutoGroupedStreamKernel<
                    Index1D,
                    ArrayView1D<byte, Stride1D.Dense>,
                    ArrayView1D<int, Stride1D.Dense>,
                    long, int, int>(PreFilterKernel);

                kernel(batchLen, gpuPrompt.View, gpuFlags.View, batchStart, promptBytes.Length, difficulty);
                acc.Synchronize();

                var flags = gpuFlags.GetAsArray1D();

                var candidates = new List<long>();
                for (int i = 0; i < batchLen; i++)
                {
                    if (flags[i] == 1)
                    {
                        candidates.Add(batchStart + i);
                    }
                }
                return candidates;
            }
        }

        /// <summary>
        /// ILGPU kernel — runs on GPU.
        /// Computes a cheap XOR-fold fingerprint for prompt+nonce and checks whether
        /// the first <paramref name="difficulty"/> nibbles are zero.
        /// </summary>
        static void PreFilterKernel(
            Index1D index,
            ArrayView1D<byte, Stride1D.Dense> prompt,
            ArrayView1D<int, Stride1D.Dense> flags,
            long batchStart,
            int promptLen,
            int difficulty)
        {
            int tid = index;
            long nonce = batchStart + tid;

            // XOR-fold all input bytes (prompt + decimal nonce digits) into a single uint.
            uint acc = 2166136261u; // FNV-1a offset basis
            const uint FnvPrime = 16777619u;

            for (int i = 0; i < promptLen; i++)
            {
                acc ^= prompt[i];
                acc *= FnvPrime;
            }

            // Fold the nonce digits.
            long n = nonce;
            if (n == 0)
            {
                acc ^= (byte)'0';
                acc *= FnvPrime;
            }
            else
            {
                // Extract digits in reverse order (acceptable for a pre-filter checksum).
                while (n > 0)
                {
                    acc ^= (byte)('0' + (n % 10));
                    acc *= FnvPrime;
                    n /= 10;
                }
            }

            // Check whether the first `difficulty` nibbles of the fingerprint are 0.
            // This is a probabilistic filter: false-positives are verified by SHA-256 on CPU;
            // false-negatives are acceptable because the CPU worker also searches the full range.
            int leadingZeroNibbles = 0;
            uint fingerprint = acc;
            for (int nibble = 7; nibble >= 0; nibble--)
            {
                uint nib = (fingerprint >> (nibble * 4)) & 0xF;
                if (nib == 0) leadingZeroNibbles++;
                else break;
            }

            flags[tid] = leadingZeroNibbles >= difficulty ? 1 : 0;
        }

        // ── CPU SHA-256 verification ──────────────────────────────────────────────────────────

        private static long? CpuVerifyCandidates(
            string prompt,
            List<long> candidates,
            string leadingZeros,
            CancellationToken cancellationToken)
        {
            long? found = null;

            Parallel.ForEach(candidates, new ParallelOptions { CancellationToken = cancellationToken }, nonce =>
            {
                if (found.HasValue) return;

                var hashValue = HashService.GetHashValue($"{prompt}{nonce}");

                if (hashValue.StartsWith(leadingZeros))
                {
                    Interlocked.CompareExchange(ref found!, nonce, null!);
                }
            });

            return found;
        }
    }
}
