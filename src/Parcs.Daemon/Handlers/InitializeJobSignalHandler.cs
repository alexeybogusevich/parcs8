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
            var jobId = await managedChannel.ReadLongAsync();

            var isExistingJob = _jobContextAccessor.TryGet(jobId, out _);
            await managedChannel.WriteDataAsync(isExistingJob);

            if (isExistingJob)
            {
                return;
            }

            var moduleId = await managedChannel.ReadLongAsync();
            var pointsNumber = await managedChannel.ReadIntAsync();
            var arguments = await managedChannel.ReadObjectAsync<IDictionary<string, string>>();

            _jobContextAccessor.Add(jobId, moduleId, pointsNumber, arguments);
            _ = _jobContextAccessor.TryGet(jobId, out var jobContext);

            managedChannel.SetCancellation(jobContext.CancellationTokenSource.Token);
        }
    }
}