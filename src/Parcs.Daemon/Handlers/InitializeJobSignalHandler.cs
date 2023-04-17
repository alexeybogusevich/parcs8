using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public sealed class InitializeJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public InitializeJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadGuidAsync();
            var moduleId = await managedChannel.ReadGuidAsync();

            var pointsNumber = await managedChannel.ReadIntAsync();
            var arguments = await managedChannel.ReadObjectAsync<IDictionary<string, string>>();

            _jobContextAccessor.Current?.CancellationTokenSource.Cancel();
            _jobContextAccessor.Set(jobId, moduleId, pointsNumber, arguments);

            managedChannel.SetCancellation(_jobContextAccessor.Current.CancellationTokenSource.Token);
        }
    }
}