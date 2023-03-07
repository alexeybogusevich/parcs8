namespace Parcs.Core
{
    public interface IChannelTransmissonManager
    {
        long Send(byte[] bytes);
        long Receive(byte[] bytes);
    }
}