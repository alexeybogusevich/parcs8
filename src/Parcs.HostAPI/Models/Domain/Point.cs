using Parcs.Net;
using Parcs.Shared.Models;
using System.Net.Sockets;

namespace Parcs.TCP.Host.Models
{
    internal sealed class Point : IPoint, IDisposable
    {
        private TcpClient _tcpClient;
        private Channel _createdChannel;

        private readonly Guid _jobId;
        private readonly string _workerModulesPath;
        private readonly CancellationToken _cancellationToken;

        public Point(TcpClient tcpClient, Guid jobId, string workerModulesPath, CancellationToken cancellationToken)
        {
            _tcpClient = tcpClient;
            _jobId = jobId;
            _workerModulesPath = workerModulesPath;
            _cancellationToken = cancellationToken;
        }

        public async Task<IChannel> CreateChannelAsync()
        {
            if (_createdChannel is not null)
            {
                return _createdChannel;
            }

            var networkStream = _tcpClient.GetStream();

            _createdChannel = new Channel(networkStream);
            _createdChannel.SetCancellation(_cancellationToken);

            await _createdChannel.WriteSignalAsync(Signal.InitializeJob);
            await _createdChannel.WriteDataAsync(_jobId);
            await _createdChannel.WriteDataAsync(_workerModulesPath);

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

        public async Task DeleteAsync()
        {
            await _createdChannel.WriteSignalAsync(Signal.CloseConnection);
            Dispose();
        }

        public void Dispose()
        {
            if (_tcpClient is not null)
            {
                _tcpClient.Dispose();
                _tcpClient = null;
            }

            if (_createdChannel is not null)
            {
                _createdChannel.Dispose();
                _createdChannel = null;
            }
        }
    }
}