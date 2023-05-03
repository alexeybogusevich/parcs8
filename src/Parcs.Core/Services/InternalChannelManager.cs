using Parcs.Core.Models;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Parcs.Core.Services
{
    public class InternalChannelManager : IInternalChannelManager
    {
        private readonly ConcurrentDictionary<Guid, Tuple<InternalChannel, InternalChannel>> _activeInternalChannels = new ();
        private readonly ChannelWriter<InternalChannelReference> _channelWriter;

        public InternalChannelManager(ChannelWriter<InternalChannelReference> channelWriter)
        {
            _channelWriter = channelWriter;
        }

        public Guid Create()
        {
            var channelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = true };

            var firstChannel = Channel.CreateUnbounded<IInternalChannelData>(channelOptions);
            var secondChannel = Channel.CreateUnbounded<IInternalChannelData>(channelOptions);

            var internalChannelId = Guid.NewGuid();

            var firstInternalChannel = new InternalChannel(secondChannel.Reader, firstChannel.Writer, () => Remove(internalChannelId));
            var secondInternalChannel = new InternalChannel(firstChannel.Reader, secondChannel.Writer, () => Remove(internalChannelId));

            _ = _activeInternalChannels.TryAdd(internalChannelId, new Tuple<InternalChannel, InternalChannel>(firstInternalChannel, secondInternalChannel));
            _ = _channelWriter.TryWrite(new InternalChannelReference(internalChannelId));

            return internalChannelId;
        }

        public void Remove(Guid id) => _activeInternalChannels.TryRemove(id, out _);

        public bool TryGet(Guid id, out Tuple<InternalChannel, InternalChannel> channel) => _activeInternalChannels.TryGetValue(id, out channel);
    }
}