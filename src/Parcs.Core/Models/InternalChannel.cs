using Parcs.Core.Models.Interfaces;
using Parcs.Net;
using System.Threading.Channels;

namespace Parcs.Core.Models
{
    public sealed class InternalChannel : IManagedChannel
    {
        private CancellationToken _cancellationToken = default;
        private readonly Channel<IInternalChannelData> _channel;
        private readonly Action _disposeAction;

        public InternalChannel(Channel<IInternalChannelData> channel, Action disposeAction)
        {
            _channel = channel;
            _disposeAction = disposeAction;
        }

        public void SetCancellation(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public ValueTask WriteSignalAsync(Signal signal)
        {
            var internalChannelData = new InternalChannelData<Signal>(signal);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(bool data)
        {
            var internalChannelData = new InternalChannelData<bool>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(byte data)
        {
            var internalChannelData = new InternalChannelData<byte>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(byte[] data)
        {
            var internalChannelData = new InternalChannelData<byte[]>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(int data)
        {
            var internalChannelData = new InternalChannelData<int>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(long data)
        {
            var internalChannelData = new InternalChannelData<long>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(double data)
        {
            var internalChannelData = new InternalChannelData<double>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(string data)
        {
            var internalChannelData = new InternalChannelData<string>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteDataAsync(Guid data)
        {
            var internalChannelData = new InternalChannelData<Guid>(data);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public ValueTask WriteObjectAsync<T>(T @object)
        {
            var internalChannelData = new InternalChannelData<T>(@object);
            return _channel.Writer.WriteAsync(internalChannelData, _cancellationToken);
        }

        public async Task<Signal> ReadSignalAsync()
        {
            var internalChannelData = await TryReceiveAsync<Signal>();
            return internalChannelData.Payload;
        }

        public async Task<bool> ReadBooleanAsync()
        {
            var internalChannelData = await TryReceiveAsync<bool>();
            return internalChannelData.Payload;
        }

        public async Task<byte> ReadByteAsync()
        {
            var internalChannelData = await TryReceiveAsync<byte>();
            return internalChannelData.Payload;
        }

        public async Task<byte[]> ReadBytesAsync()
        {
            var internalChannelData = await TryReceiveAsync<byte[]>();
            return internalChannelData.Payload;
        }

        public async Task<int> ReadIntAsync()
        {
            var internalChannelData = await TryReceiveAsync<int>();
            return internalChannelData.Payload;
        }

        public async Task<long> ReadLongAsync()
        {
            var internalChannelData = await TryReceiveAsync<long>();
            return internalChannelData.Payload;
        }

        public async Task<double> ReadDoubleAsync()
        {
            var internalChannelData = await TryReceiveAsync<double>();
            return internalChannelData.Payload;
        }

        public async Task<string> ReadStringAsync()
        {
            var internalChannelData = await TryReceiveAsync<string>();
            return internalChannelData.Payload;
        }

        public async Task<Guid> ReadGuidAsync()
        {
            var internalChannelData = await TryReceiveAsync<Guid>();
            return internalChannelData.Payload;
        }

        public async Task<T> ReadObjectAsync<T>()
        {
            var internalChannelData = await TryReceiveAsync<T>();
            return internalChannelData.Payload;
        }

        private async Task<InternalChannelData<T>> TryReceiveAsync<T>()
        {
            var internalChannelData = await _channel.Reader.ReadAsync(_cancellationToken);

            if (internalChannelData is not InternalChannelData<T> typedInternalChannelData)
            {
                throw new ArgumentException($"Expected to receive {typeof(T).FullName} but got {internalChannelData.GetType().FullName}");
            }

            return typedInternalChannelData;
        }

        public void Dispose() => _disposeAction.Invoke();
    }
}