namespace Parcs.Core
{
    public interface ITransmissonManager
    {
        long Send(byte[] bytes);
        long Receive(byte[] bytes);
    }
}