using Parcs.Net;
using Parcs.Shared.Models;
using Parcs.TCP.Host.Models;
using System.Net.Sockets;

namespace Parcs.HostAPI.Models.Domain
{
    internal sealed class HostInfo : IHostInfo
    {
        private readonly Job _job;
        private readonly string _workerModulesPath;
        private readonly IInputReader _inputReader;
        private readonly IOutputWriter _outputWriter;
        private readonly Queue<Daemon> _availableDaemons;

        public HostInfo(
            Job job,
            IEnumerable<Daemon> availableDaemons,
            string workerModulesPath,
            IInputReader inputReader,
            IOutputWriter outputWriter)
        {
            _job = job;
            _workerModulesPath = workerModulesPath;
            _inputReader = inputReader;
            _outputWriter = outputWriter;
            _availableDaemons = new Queue<Daemon>(availableDaemons);
        }

        public int AvailablePointsNumber => _availableDaemons.Count;

        public IInputReader GetInputReader() => _inputReader;

        public IOutputWriter GetOutputWriter() => _outputWriter;

        public async Task<IPoint> CreatePointAsync()
        {
            if (!_availableDaemons.TryDequeue(out var daemon))
            {
                throw new ArgumentException("No more points can be created.");
            }

            _job.TrackExecution(daemon);

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(daemon.HostUrl, daemon.Port);

            return new Point(tcpClient, _job.Id, _workerModulesPath, _job.CancellationToken);
        }
    }
}