using Parcs.Daemon.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IHostApiClient
    {
        Task PostJobFailureAsync(PostJobFailureApiRequest request, CancellationToken cancellationToken = default);
    }
}