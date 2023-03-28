using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobCompletionNotifier
    {
        Task NotifyAsync(JobCompletionNotification notification, string subscriberUrl, CancellationToken cancellationToken = default);
    }
}