using Parcs.Net;
using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;

namespace Parcs.Core.Models
{
    public sealed class ModuleInfo : IModuleInfo
    {
        private readonly Guid _jobId;
        private readonly Guid _moduleId;
        private readonly CancellationToken _cancellationToken;

        private readonly List<Point> _createdPoints = new ();
        private readonly Dictionary<string, int> _pointsOnDaemons = new();
        private readonly IDaemonResolver _daemonResolver;
        private readonly IInternalChannelManager _internalChannelManager;
        private readonly IAddressResolver _addressResolver;

        public ModuleInfo(
            Guid jobId,
            Guid moduleId,
            IChannel parentChannel,
            IInputOutputFactory inputOutputFactory,
            IArgumentsProvider argumentsProvider,
            IDaemonResolver daemonResolver,
            IInternalChannelManager internalChannelManager,
            IAddressResolver addressResolver,
            CancellationToken cancellationToken)
        {
            _jobId = jobId;
            _moduleId = moduleId;
            _daemonResolver = daemonResolver;
            _internalChannelManager = internalChannelManager;
            _addressResolver = addressResolver;
            _cancellationToken = cancellationToken;
            Parent = parentChannel;
            InputReader = inputOutputFactory.CreateReader(jobId);
            OutputWriter = inputOutputFactory.CreateWriter(jobId, cancellationToken);
            ArgumentsProvider = argumentsProvider;
        }

        public IChannel Parent { get; }

        public IInputReader InputReader { get; }

        public IOutputWriter OutputWriter { get; }

        public IArgumentsProvider ArgumentsProvider { get; }

        public async Task<IPoint> CreatePointAsync()
        {
            var nextDaemon = GetNextDaemon();

            var nextDaemonAddresses = _addressResolver.Resolve(nextDaemon.HostUrl);

            if (nextDaemonAddresses.Any(IPAddress.IsLoopback))
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

            _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannel);
            internalChannel.SetCancellation(_cancellationToken);

            var internalPoint = new Point(_jobId, _moduleId, internalChannel, ArgumentsProvider);
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