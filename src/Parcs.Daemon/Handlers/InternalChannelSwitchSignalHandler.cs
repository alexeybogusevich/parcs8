using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    public class InternalChannelSwitchSignalHandler : ISignalHandler
    {
        private readonly IInternalChannelManager _internalChannelManager;

        public InternalChannelSwitchSignalHandler(IInternalChannelManager internalChannelManager)
        {
            _internalChannelManager = internalChannelManager;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var internalChannelId = await managedChannel.ReadGuidAsync();

            if (!_internalChannelManager.TryGet(internalChannelId, out var internalChannel))
            {
                throw new ArgumentException($"Couldn't find an internal channel with id {internalChannelId}");
            }

            await managedChannel.WriteSignalAsync(Signal.InternalChannelSwitch);

            managedChannel.Dispose();

            managedChannel = internalChannel;

            managedChannel.SetCancellation(cancellationToken);
        }
    }
}