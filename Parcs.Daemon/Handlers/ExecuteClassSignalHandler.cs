using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    public sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;
        private readonly ITypeLoader<IWorkerModule> _typeLoader;

        public ExecuteClassSignalHandler(IJobContextAccessor jobContextAccessor, ITypeLoader<IWorkerModule> typeLoader)
        {
            _jobContextAccessor = jobContextAccessor;
            _typeLoader = typeLoader;
        }

        public async Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            if (_jobContextAccessor.Current is null)
            {
                throw new ApplicationException("No job has been initialized.");
            }

            var workerModulesPath = _jobContextAccessor.Current.WorkerModulesPath;
            var jobCancellationToken = _jobContextAccessor.Current.CancellationTokenSource.Token;

            try
            {
                var assemblyName = await channel.ReadStringAsync(jobCancellationToken);
                var className = await channel.ReadStringAsync(jobCancellationToken);
                var workerModule = _typeLoader.Load(workerModulesPath, assemblyName, className);

                await workerModule.RunAsync(channel, jobCancellationToken);
            }
            finally
            {
                _jobContextAccessor.Reset();
            }
        }
    }
}