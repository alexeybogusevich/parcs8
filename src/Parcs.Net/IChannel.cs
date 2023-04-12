namespace Parcs.Net
{
    public interface IChannel : IDisposable
    {
        ValueTask WriteSignalAsync(Signal signal);
        ValueTask WriteDataAsync(bool data);
        ValueTask WriteDataAsync(byte data);
        ValueTask WriteDataAsync(byte[] data);
        ValueTask WriteDataAsync(int data);
        ValueTask WriteDataAsync(long data);
        ValueTask WriteDataAsync(double data);
        ValueTask WriteDataAsync(string data);
        ValueTask WriteDataAsync(Guid data);
        ValueTask WriteObjectAsync<T>(T @object);
        Task<Signal> ReadSignalAsync();
        Task<bool> ReadBooleanAsync();
        Task<byte> ReadByteAsync();
        Task<byte[]> ReadBytesAsync();
        Task<int> ReadIntAsync();
        Task<long> ReadLongAsync();
        Task<double> ReadDoubleAsync();
        Task<string> ReadStringAsync();
        Task<Guid> ReadGuidAsync();
        Task<T> ReadObjectAsync<T>();
    }
}