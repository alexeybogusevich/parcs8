namespace Parcs.Daemon.Services.Interfaces
{
    public interface IHostApiClient
    {
        Task PutCancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    }
}