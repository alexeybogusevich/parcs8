using Parcs.Core;
using Parcs.Modules.Sample;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var assemblyName = channel.ReadStringAsync(cancellationToken);
            var className = channel.ReadStringAsync(cancellationToken);

            var sampleModule = new SampleWorkerModule();

            return sampleModule.RunAsync(channel, cancellationToken);
        }
    }
}