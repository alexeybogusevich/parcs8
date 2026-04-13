namespace Parcs.Daemon.Services.Interfaces
{
    /// <summary>
    /// Provides information about GPU availability in the current pod/node.
    /// Consumed by hosted services and log start-up diagnostics.
    /// </summary>
    public interface IGpuAvailabilityService
    {
        /// <summary>
        /// <c>true</c> if at least one CUDA-capable GPU was detected at process start.
        /// <c>false</c> if no CUDA runtime is present (CPU-only node or local development).
        /// </summary>
        bool IsCudaAvailable { get; }

        /// <summary>Human-readable description of the detected accelerator.</summary>
        string AcceleratorDescription { get; }
    }
}
