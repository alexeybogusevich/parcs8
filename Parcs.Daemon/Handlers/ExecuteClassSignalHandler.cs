using Parcs.Modules.Sample;
using Parcs.Net;
using Parcs.Shared.Services.Interfaces;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        private readonly ITypeLoader<IWorkerModule> _typeLoader;

        public ExecuteClassSignalHandler(ITypeLoader<IWorkerModule> typeLoader)
        {
            _typeLoader = typeLoader;
        }

        public async Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var assemblyName = await channel.ReadStringAsync(cancellationToken);
            var className = await channel.ReadStringAsync(cancellationToken);

            var sampleModule = new SampleWorkerModule();

            await sampleModule.RunAsync(channel, cancellationToken);
        }
    }
}