using Parcs.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Parcs.Shared.Models
{
    public sealed class Channel : IChannel, IDisposable
    {
        private NetworkStream _networkStream;

        public Channel(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }

        public async Task<Signal> ReadSignalAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(byte);
            var bytes = await TryReceiveAsync(size, cancellationToken);
            return (Signal)bytes[0];
        }

        public async Task<bool> ReadBooleanAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(bool);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return BitConverter.ToBoolean(buffer);
        }

        public async Task<byte> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(byte);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return buffer[0];
        }

        public async Task<double> ReadDoubleAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(double);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return BitConverter.ToDouble(buffer);
        }

        public async Task<int> ReadIntAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(int);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return BitConverter.ToInt32(buffer);
        }

        public async Task<long> ReadLongAsync(CancellationToken cancellationToken = default)
        {
            var size = sizeof(long);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return BitConverter.ToInt64(buffer);
        }

        public async Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default)
        {
            var size = await ReadIntAsync(cancellationToken);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            using var memoryStream = new MemoryStream(buffer.ToArray());
            return JsonSerializer.Deserialize<T>(memoryStream);
        }

        public async Task<string> ReadStringAsync(CancellationToken cancellationToken = default)
        {
            var size = await ReadIntAsync(cancellationToken);
            var buffer = await TryReceiveAsync(size, cancellationToken);
            return Encoding.UTF8.GetString(buffer);
        }

        public ValueTask WriteSignalAsync(Signal signal, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[] { (byte)signal };
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask WriteDataAsync(bool data, CancellationToken cancellationToken = default)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask WriteDataAsync(byte data, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[] { data };
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask WriteDataAsync(int data, CancellationToken cancellationToken = default)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask WriteDataAsync(long data, CancellationToken cancellationToken = default)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask WriteDataAsync(double data, CancellationToken cancellationToken = default)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public async ValueTask WriteDataAsync(string data, CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await WriteDataAsync(bytes.Length, cancellationToken);
            await _networkStream.WriteAsync(bytes, cancellationToken);
        }

        public async ValueTask WriteObjectAsync<T>(T @object, CancellationToken cancellationToken = default)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(@object);
            await WriteDataAsync(bytes.Length, cancellationToken);
            await _networkStream.WriteAsync(bytes, cancellationToken);
        }

        private async Task<byte[]> TryReceiveAsync(int size, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[size];

            var length = await _networkStream.ReadAsync(buffer.AsMemory(0, size), cancellationToken);

            if (length != size)
            {
                throw new ArgumentException($"Expected to receive {size} bytes, but got {length}.");
            }

            return buffer;
        }

        public void Dispose()
        {
            _networkStream.Dispose();
            _networkStream = null;
        }
    }
}