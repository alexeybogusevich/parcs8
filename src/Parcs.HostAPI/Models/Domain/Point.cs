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

        public Point(TcpClient tcpClient, Guid jobId, string workerModulesPath)
        {
            _tcpClient = tcpClient;
            _jobId = jobId;
            _workerModulesPath = workerModulesPath;
        }

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_createdChannel is not null)
            {
                return _createdChannel;
            }

            var networkStream = _tcpClient.GetStream();
            _createdChannel = new Channel(networkStream);

            await _createdChannel.WriteSignalAsync(Signal.InitializeJob, cancellationToken);
            await _createdChannel.WriteDataAsync(_jobId, cancellationToken);
            await _createdChannel.WriteDataAsync(_workerModulesPath, cancellationToken);

            return _createdChannel;
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