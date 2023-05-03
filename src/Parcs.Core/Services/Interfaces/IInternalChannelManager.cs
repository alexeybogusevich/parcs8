using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    public interface IInternalChannelManager
    {
        bool TryGet(Guid id, out Tuple<InternalChannel, InternalChannel> channel);
        Guid Create();
        void Remove(Guid id);
    }
}