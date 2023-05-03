using Microsoft.Extensions.Hosting;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using System.Threading.Channels;

namespace Parcs.Daemon.HostedServices
{
    public class InternalServer : IHostedService
    {
        private readonly ChannelReader<InternalChannelReference> _channelReader;
        private readonly IInternalChannelManager _internalChannelManager;
        private readonly IChannelOrchestrator _channelOrchestrator;

        public InternalServer(
            ChannelReader<InternalChannelReference> channelReader, IInternalChannelManager internalChannelManager, IChannelOrchestrator channelOrchestrator)
        {
            _channelReader = channelReader;
            _internalChannelManager = internalChannelManager;
            _channelOrchestrator = channelOrchestrator;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await foreach (var internalChannelReference in _channelReader.ReadAllAsync(cancellationToken))
            {
                if (!_internalChannelManager.TryGet(internalChannelReference.Id, out var internalChannel))
                {
                    continue;
                }

                _ = Task.Run(async () => await _channelOrchestrator.OrchestrateAsync(internalChannel, cancellationToken), cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}