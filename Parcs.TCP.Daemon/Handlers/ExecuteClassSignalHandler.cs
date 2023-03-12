using Parcs.Core;
using Parcs.Daemon.Modules;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var sampleModule = new WorkerModuleSample();
            return sampleModule.RunAsync(channel, cancellationToken);
        }
    }
}