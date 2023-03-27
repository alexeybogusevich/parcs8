using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    public class CancelJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public CancelJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            _jobContextAccessor.Current?.CancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
    }
}