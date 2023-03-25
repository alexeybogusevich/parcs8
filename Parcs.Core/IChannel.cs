namespace Parcs.Net
{
    public interface IChannel
    {
        ValueTask WriteSignalAsync(Signal signal, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(bool data, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(byte data, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(int data, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(long data, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(double data, CancellationToken cancellationToken = default);
        ValueTask WriteDataAsync(string data, CancellationToken cancellationToken = default);
        ValueTask WriteObjectAsync<T>(T @object, CancellationToken cancellationToken = default);
        Task<Signal> ReadSignalAsync(CancellationToken cancellationToken = default);
        Task<bool> ReadBooleanAsync(CancellationToken cancellationToken = default);
        Task<byte> ReadByteAsync(CancellationToken cancellationToken = default);
        Task<int> ReadIntAsync(CancellationToken cancellationToken = default);
        Task<long> ReadLongAsync(CancellationToken cancellationToken = default);
        Task<double> ReadDoubleAsync(CancellationToken cancellationToken = default);
        Task<string> ReadStringAsync(CancellationToken cancellationToken = default);
        Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default);
    }
}