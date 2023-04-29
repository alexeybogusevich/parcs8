using Parcs.Core.Models.Interfaces;
using Parcs.Net;

namespace Parcs.Core.Models
{
    public sealed class InternalChannel : IManagedChannel
    {
        private CancellationToken _cancellationToken = default;

        public InternalChannel()
        {
            throw new NotImplementedException();
        }

        public void SetCancellation(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public ValueTask WriteSignalAsync(Signal signal)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(bool data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(byte data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(byte[] data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(int data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(long data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(double data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(string data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteDataAsync(Guid data)
        {
            throw new NotImplementedException();
        }

        public ValueTask WriteObjectAsync<T>(T @object)
        {
            throw new NotImplementedException();
        }

        public Task<Signal> ReadSignalAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReadBooleanAsync()
        {
            throw new NotImplementedException();
        }

        public Task<byte> ReadByteAsync()
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> ReadBytesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<int> ReadIntAsync()
        {
            throw new NotImplementedException();
        }

        public Task<long> ReadLongAsync()
        {
            throw new NotImplementedException();
        }

        public Task<double> ReadDoubleAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> ReadStringAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Guid> ReadGuidAsync()
        {
            throw new NotImplementedException();
        }

        public Task<T> ReadObjectAsync<T>()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}