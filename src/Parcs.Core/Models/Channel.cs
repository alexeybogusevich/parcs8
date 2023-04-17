using Parcs.Net;
using Parcs.Core.Models.Interfaces;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Parcs.Core.Models
{
    public sealed class Channel : IManagedChannel
    {
        private NetworkStream _networkStream;
        private CancellationToken _cancellationToken = default;

        public Channel(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }

        public void SetCancellation(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public async Task<Signal> ReadSignalAsync()
        {
            var size = sizeof(byte);
            var bytes = await TryReceiveAsync(size);
            return (Signal)bytes[0];
        }

        public async Task<bool> ReadBooleanAsync()
        {
            var size = sizeof(bool);
            var buffer = await TryReceiveAsync(size);
            return BitConverter.ToBoolean(buffer);
        }

        public async Task<byte> ReadByteAsync()
        {
            var size = sizeof(byte);
            var buffer = await TryReceiveAsync(size);
            return buffer[0];
        }

        public async Task<byte[]> ReadBytesAsync()
        {
            var size = await ReadIntAsync();
            return await TryReceiveAsync(size);
        }

        public async Task<double> ReadDoubleAsync()
        {
            var size = sizeof(double);
            var buffer = await TryReceiveAsync(size);
            return BitConverter.ToDouble(buffer);
        }

        public async Task<int> ReadIntAsync()
        {
            var size = sizeof(int);
            var buffer = await TryReceiveAsync(size);
            return BitConverter.ToInt32(buffer);
        }

        public async Task<long> ReadLongAsync()
        {
            var size = sizeof(long);
            var buffer = await TryReceiveAsync(size);
            return BitConverter.ToInt64(buffer);
        }

        public async Task<Guid> ReadGuidAsync()
        {
            var size = await ReadIntAsync();
            var buffer = await TryReceiveAsync(size);
            return new Guid(buffer);
        }

        public async Task<T> ReadObjectAsync<T>()
        {
            var size = await ReadIntAsync();
            var buffer = await TryReceiveAsync(size);
            using var memoryStream = new MemoryStream(buffer.ToArray());
            return JsonSerializer.Deserialize<T>(memoryStream);
        }

        public async Task<string> ReadStringAsync()
        {
            var size = await ReadIntAsync();
            var buffer = await TryReceiveAsync(size);
            return Encoding.UTF8.GetString(buffer);
        }

        public ValueTask WriteSignalAsync(Signal signal)
        {
            var bytes = new byte[] { (byte)signal };
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public ValueTask WriteDataAsync(bool data)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public ValueTask WriteDataAsync(byte data)
        {
            var bytes = new byte[] { data };
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public async ValueTask WriteDataAsync(byte[] data)
        {
            await WriteDataAsync(data.Length);
            await _networkStream.WriteAsync(data, _cancellationToken);
        }

        public ValueTask WriteDataAsync(int data)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public ValueTask WriteDataAsync(long data)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public ValueTask WriteDataAsync(double data)
        {
            var bytes = BitConverter.GetBytes(data);
            return _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public async ValueTask WriteDataAsync(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await WriteDataAsync(bytes.Length);
            await _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public async ValueTask WriteDataAsync(Guid data)
        {
            var bytes = data.ToByteArray();
            await WriteDataAsync(bytes.Length);
            await _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        public async ValueTask WriteObjectAsync<T>(T @object)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(@object);
            await WriteDataAsync(bytes.Length);
            await _networkStream.WriteAsync(bytes, _cancellationToken);
        }

        private async Task<byte[]> TryReceiveAsync(int size)
        {
            var buffer = new byte[size];

            int offset = 0;
            int count = size;

            while (offset < size)
            {
                var bytesRead = await _networkStream.ReadAsync(buffer.AsMemory(offset, count), _cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
                count -= bytesRead;
            }

            if (offset != size)
            {
                throw new ArgumentException($"Expected to receive {size} bytes but got {offset}");
            }

            return buffer;
        }

        public void Dispose()
        {
            if (_networkStream is not null)
            {
                _networkStream.Dispose();
                _networkStream = null;
            }
        }
    }
}