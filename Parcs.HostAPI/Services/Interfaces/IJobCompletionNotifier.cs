using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobCompletionNotifier
    {
        Task NotifyAsync(JobCompletionNotification response, string subscriberUrl, CancellationToken cancellationToken = default);
    }
}