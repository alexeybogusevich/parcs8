using Parcs.Host.Models.Domain;

namespace Parcs.Host.Services.Interfaces
{
    public interface IJobCompletionNotifier
    {
        Task NotifyAsync(JobCompletionNotification notification, string subscriberUrl, CancellationToken cancellationToken = default);
    }
}