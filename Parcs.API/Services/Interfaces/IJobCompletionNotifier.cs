using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobCompletionNotifier
    {
        Task NotifyAsync(RunJobCommandResponse response, string callbackUrl, CancellationToken cancellationToken = default);
    }
}