namespace Parcs.Core
{
    public interface IChannelTransmissonManager
    {
        long Send(byte[] bytes);
        long Send(string value);
        long Receive(byte[] bytes);
        string Receive(long size);
    }
}