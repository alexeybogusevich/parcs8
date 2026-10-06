namespace Parcs.Core.Services.Interfaces
{
    /// <summary>
    /// Decides which daemon pool (CPU or GPU) a job's points are provisioned on. The Host reads
    /// it from the job record; a daemon inherits it from the point request it is serving.
    /// </summary>
    public interface IJobPlacementResolver
    {
        Task<bool> RequiresGpuAsync(long jobId, CancellationToken cancellationToken = default);
    }
}
