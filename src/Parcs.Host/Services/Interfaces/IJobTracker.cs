namespace Parcs.Host.Services.Interfaces
{
    public interface IJobTracker
    {
        void StartTracking(long jobId);
        bool TryGetCancellationToken(long jobId, out CancellationToken cancellationToken);
        Task<bool> CancelAndStopTrackingAsync(long jobId);
        bool StopTracking(long jobId);
    }
}