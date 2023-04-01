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
            await _createdChannel.WriteDataAsync(_jobId, CancellationToken.None);
            await _createdChannel.WriteDataAsync(_workerModulesPath, CancellationToken.None);

            return _createdChannel;
        }

        public async Task ExecuteClassAsync(string assemblyName, string className, CancellationToken cancellationToken = default)
        {
            if (_createdChannel is null)
            {
                throw new ArgumentException("No channel has been created.");
            }

            await _createdChannel.WriteSignalAsync(Signal.ExecuteClass, cancellationToken);
            await _createdChannel.WriteDataAsync(assemblyName, CancellationToken.None);
            await _createdChannel.WriteDataAsync(className, CancellationToken.None);
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