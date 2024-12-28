using Parcs.Net;
using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;

namespace Parcs.Core.Models
{
    public sealed class ModuleInfo(
        JobMetadata jobMetadata,
        IChannel parentChannel,
        IInputOutputFactory inputOutputFactory,
        IArgumentsProvider argumentsProvider,
        IDaemonResolver daemonResolver,
        IInternalChannelManager internalChannelManager,
        IAddressResolver addressResolver,
        CancellationToken cancellationToken) : IModuleInfo
    {
        private readonly long _jobId = jobMetadata.JobId;
        private readonly long _moduleId = jobMetadata.ModuleId;

        private readonly List<Point> _createdPoints = [];
        private readonly Dictionary<string, int> _pointsOnDaemons = [];
        private readonly CancellationToken _cancellationToken = cancellationToken;

        private readonly IDaemonResolver _daemonResolver = daemonResolver;
        private readonly IInternalChannelManager _internalChannelManager = internalChannelManager;
        private readonly IAddressResolver _addressResolver = addressResolver;

        public bool IsHost => Parent is null;

        public IChannel Parent { get; } = parentChannel;

        public IInputReader InputReader { get; } = inputOutputFactory.CreateReader(jobMetadata.JobId);

        public IOutputWriter OutputWriter { get; } = inputOutputFactory.CreateWriter(jobMetadata.JobId, cancellationToken);

        public IArgumentsProvider ArgumentsProvider { get; } = argumentsProvider;

        public async Task<IPoint> CreatePointAsync()
        {
            var nextDaemon = GetNextDaemon();

            var nextDaemonAddresses = _addressResolver.Resolve(nextDaemon.HostUrl);

            if (IsHost is false && nextDaemonAddresses.Any(IPAddress.IsLoopback))
            {
                return CreateInternalPoint();
            }

            return await CreateNetworkPointAsync(nextDaemonAddresses, nextDaemon.Port);
        }

        private async Task<IPoint> CreateNetworkPointAsync(IPAddress[] nextDaemonAddresses, int daemonPort)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(nextDaemonAddresses, daemonPort);

            var networkChannel = new NetworkChannel(tcpClient);
            networkChannel.SetCancellation(_cancellationToken);

            var networkPoint = new Point(_jobId, _moduleId, networkChannel, ArgumentsProvider);
            _createdPoints.Add(networkPoint);

            return networkPoint;
        }

        private IPoint CreateInternalPoint()
        {
            var internalChannelId = _internalChannelManager.Create();

            _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannelPair);
            internalChannelPair.Item1.SetCancellation(_cancellationToken);

            var internalPoint = new Point(_jobId, _moduleId, internalChannelPair.Item1, ArgumentsProvider);
            _createdPoints.Add(internalPoint);

            return internalPoint;
        }

        private Daemon GetNextDaemon()
        {
            var availableDaemons = _daemonResolver.GetAvailableDaemons();

            if (availableDaemons is null || !availableDaemons.Any())
            {
                throw new InvalidOperationException("No daemons available.");
            }

            foreach (var daemon in availableDaemons.Where(daemon => !_pointsOnDaemons.ContainsKey(daemon.HostUrl)))
            {
                _pointsOnDaemons.TryAdd(daemon.HostUrl, 0);
            }

            var leastPointsDaemon = _pointsOnDaemons.FirstOrDefault(d => d.Value == _pointsOnDaemons.Min(d => d.Value));

            return availableDaemons.FirstOrDefault(d => d.HostUrl == leastPointsDaemon.Key);
        }

        public async ValueTask DisposeAsync()
        {
            if (_createdPoints.Any() is false)
            {
                return;
            }

            foreach (var point in _createdPoints)
            {
                await point.DisposeAsync();
            }

            _createdPoints.Clear();
        }
    }
}