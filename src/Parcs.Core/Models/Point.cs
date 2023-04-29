using Parcs.Net;
using System.Net.Sockets;

namespace Parcs.Core.Models
{
    public sealed class Point : IPoint
    {
        private readonly Guid _jobId;
        private readonly Guid _moduleId;
        private readonly CancellationToken _cancellationToken;

        private TcpClient _tcpClient;
        private readonly IArgumentsProvider _argumentsProvider;
        private NetworkChannel _createdChannel;

        public Point(
            Guid jobId, Guid moduleId, TcpClient tcpClient, IArgumentsProvider argumentsProvider, CancellationToken cancellationToken)
        {
            Id = Guid.NewGuid();
            _jobId = jobId;
            _moduleId = moduleId;
            _tcpClient = tcpClient;
            _argumentsProvider = argumentsProvider;
            _cancellationToken = cancellationToken;
        }

        public Guid Id { get; init; }

        public async Task<IChannel> CreateChannelAsync()
        {
            if (_createdChannel is not null)
            {
                return _createdChannel;
            }

            var networkStream = _tcpClient.GetStream();

            _createdChannel = new NetworkChannel(networkStream);
            _createdChannel.SetCancellation(_cancellationToken);

            await _createdChannel.WriteSignalAsync(Signal.InitializeJob);
            await _createdChannel.WriteDataAsync(_jobId);
            await _createdChannel.WriteDataAsync(_moduleId);
            await _createdChannel.WriteDataAsync(_argumentsProvider.GetBase().PointsNumber);
            await _createdChannel.WriteObjectAsync(_argumentsProvider.GetRaw());

            return _createdChannel;
        }

        public async Task ExecuteClassAsync(string assemblyName, string className)
        {
            if (_createdChannel is null)
            {
                throw new ArgumentException("No channel has been created.");
            }

            await _createdChannel.WriteSignalAsync(Signal.ExecuteClass);
            await _createdChannel.WriteDataAsync(assemblyName);
            await _createdChannel.WriteDataAsync(className);
        }

        public async Task DeleteAsync() => await DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            if (_createdChannel is not null)
            {
                if (_tcpClient.Connected)
                {
                    await _createdChannel.WriteSignalAsync(Signal.CloseConnection);
                }

                _createdChannel.Dispose();
                _createdChannel = null;
            }

            if (_tcpClient is not null)
            {
                _tcpClient.Dispose();
                _tcpClient = null;
            }
        }
    }
}