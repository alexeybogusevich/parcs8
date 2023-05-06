namespace Parcs.Daemon.Services.Interfaces
{
    public interface IHostApiClient
    {
        Task PutCancelJobAsync(long jobId, CancellationToken cancellationToken = default);
    }
}