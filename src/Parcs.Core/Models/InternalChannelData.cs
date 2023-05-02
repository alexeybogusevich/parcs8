using Parcs.Core.Models.Interfaces;

namespace Parcs.Core.Models
{
    public class InternalChannelData<T> : IInternalChannelData
    {
        public InternalChannelData()
        {
        }

        public InternalChannelData(T payload)
        {
            Payload = payload;
        }

        public T Payload { get; set; }
    }
}