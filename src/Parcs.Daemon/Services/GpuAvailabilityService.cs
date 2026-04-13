using ILGPU;
using ILGPU.Runtime.Cuda;
using Microsoft.Extensions.Logging;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    /// <summary>
    /// Probes for CUDA GPU availability at process start using ILGPU.
    ///
    /// Results are cached as singleton state so the probe runs exactly once per
    /// daemon pod lifetime.  ILGPU initialisation is intentionally done in the
    /// constructor (eager) rather than lazily so that GPU presence is logged as
    /// early as possible — visible in Kibana/Elasticsearch before the first job.
    ///
    /// If CUDA is unavailable (no driver, no GPU, or running in local dev without
    /// the NVIDIA runtime), the service logs a warning and reports <c>false</c>.
    /// Modules that consume <see cref="IGpuAvailabilityService"/> should fall back
    /// to CPU execution when <see cref="IsCudaAvailable"/> is <c>false</c>.
    /// </summary>
    public sealed class GpuAvailabilityService : IGpuAvailabilityService
    {
        public bool IsCudaAvailable { get; }
        public string AcceleratorDescription { get; }

        public GpuAvailabilityService(ILogger<GpuAvailabilityService> logger)
        {
            try
            {
                using var context = Context.CreateDefault();
                var cudaDevices = context.GetCudaDevices();

                if (cudaDevices.Count > 0)
                {
                    var device = cudaDevices[0];
                    IsCudaAvailable = true;
                    AcceleratorDescription =
                        $"CUDA GPU: {device.Name} " +
                        $"(compute {device.CudaArchitecture}, " +
                        $"{device.MemorySize / (1024 * 1024)} MiB VRAM)";

                    logger.LogInformation(
                        "GPU detected: {Description}. All algorithmic modules will use GPU acceleration.",
                        AcceleratorDescription);
                }
                else
                {
                    IsCudaAvailable = false;
                    AcceleratorDescription = "CPU (no CUDA device found)";

                    logger.LogWarning(
                        "No CUDA GPU detected. Algorithmic modules will fall back to CPU execution. " +
                        "Ensure the pod is scheduled on a GPU node with the nvidia.com/gpu resource.");
                }
            }
            catch (Exception ex)
            {
                // ILGPU throws if the CUDA runtime is not installed at all.
                IsCudaAvailable = false;
                AcceleratorDescription = $"CPU (CUDA probe failed: {ex.GetType().Name})";

                logger.LogWarning(
                    "GPU probe failed ({ExceptionType}: {Message}). " +
                    "Running without GPU acceleration. " +
                    "This is expected in local development or on CPU-only nodes.",
                    ex.GetType().Name, ex.Message);
            }
        }
    }
}
