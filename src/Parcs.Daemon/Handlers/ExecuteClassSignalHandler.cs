using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Models.Interfaces;
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

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            if (_jobContextAccessor.Current is null)
            {
                throw new ArgumentException("No job has been initialized.");
            }

            try
            {
                var assemblyDirectoryPath = _jobContextAccessor.Current.WorkerModulesPath;
                var assemblyName = await managedChannel.ReadStringAsync();
                var className = await managedChannel.ReadStringAsync();

                var workerModule = _typeLoader.Load(assemblyDirectoryPath, assemblyName, className);

                await workerModule.RunAsync(managedChannel);
            }
            finally
            {
                _jobContextAccessor.Reset();
            }
        }
    }
}