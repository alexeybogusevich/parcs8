using Parcs.Core.Models;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Parcs.Core.Services
{
    public class InternalChannelManager : IInternalChannelManager
    {
        private readonly ConcurrentDictionary<Guid, InternalChannel> _activeInternalChannels = new ();

        public Guid Create()
        {
            var channelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = true };
            var channel = Channel.CreateUnbounded<IInternalChannelData>(channelOptions);

            var internalChannelId = Guid.NewGuid();
            var internalChannel = new InternalChannel(channel, () => Remove(internalChannelId));

            _ = _activeInternalChannels.TryAdd(internalChannelId, internalChannel);

            return internalChannelId;
        }

        public void Remove(Guid id) => _activeInternalChannels.TryRemove(id, out _);

        public bool TryGet(Guid id, out InternalChannel channel) => _activeInternalChannels.TryGetValue(id, out channel);
    }
}