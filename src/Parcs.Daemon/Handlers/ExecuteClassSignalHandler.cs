using Parcs.Daemon.Extensions;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    public sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;
        private readonly IModuleLoader _moduleLoader;
        private readonly IModuleInfoFactory _moduleInfoFactory;

        public ExecuteClassSignalHandler(
            IJobContextAccessor jobContextAccessor, IModuleLoader moduleLoader, IModuleInfoFactory moduleInfoFactory)
        {
            _jobContextAccessor = jobContextAccessor;
            _moduleLoader = moduleLoader;
            _moduleInfoFactory = moduleInfoFactory;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            if (_jobContextAccessor.Current is null)
            {
                throw new ArgumentException("No job has been initialized.");
            }

            var (jobId, moduleId, pointsNumber, arguments, jobCancellationToken) = _jobContextAccessor.Current;

            try
            {
                var assemblyName = await managedChannel.ReadStringAsync();
                var className = await managedChannel.ReadStringAsync();

                var module = _moduleLoader.Load(moduleId, assemblyName, className);
                var moduleInfo = _moduleInfoFactory.Create(jobId, moduleId, pointsNumber, arguments, managedChannel, jobCancellationToken);

                await module.RunAsync(moduleInfo, jobCancellationToken);
            }
            catch (Exception ex)
            {
                _jobContextAccessor.Current.TrackException(ex);
            }
            finally
            {
                _jobContextAccessor.Reset();
            }
        }
    }
}