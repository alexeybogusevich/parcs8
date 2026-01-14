using Parcs.Net;
using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.Logging;

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
        ILogger logger,
        CancellationToken cancellationToken,
        IPointCreationService pointCreationService = null) : IModuleInfo
    {
        private readonly long _jobId = jobMetadata.JobId;
        private readonly long _moduleId = jobMetadata.ModuleId;

        private readonly List<Point> _createdPoints = [];
        private readonly Dictionary<string, int> _pointsOnDaemons = [];
        private readonly CancellationToken _cancellationToken = cancellationToken;

        private readonly IDaemonResolver _daemonResolver = daemonResolver;
        private readonly IInternalChannelManager _internalChannelManager = internalChannelManager;
        private readonly IAddressResolver _addressResolver = addressResolver;
        private readonly IArgumentsProvider _argumentsProvider = argumentsProvider;
        private readonly IPointCreationService _pointCreationService = pointCreationService;

        public bool IsHost => Parent is null;

        public IChannel Parent { get; } = parentChannel;

        public IInputReader InputReader { get; } = inputOutputFactory.CreateReader(jobMetadata.JobId);

        public IOutputWriter OutputWriter { get; } = inputOutputFactory.CreateWriter(jobMetadata.JobId, cancellationToken);

        public ILogger Logger { get; } = logger;

        public async Task<IPoint> CreatePointAsync()
        {
            var nextDaemon = GetNextDaemon();

            Logger.LogInformation("Resolved daemon {Daemon}", nextDaemon.HostUrl);

            var nextDaemonAddresses = _addressResolver.Resolve(nextDaemon.HostUrl);

            Logger.LogInformation("Resolved daemon address {Daemon}", nextDaemonAddresses.ToString());

            if (IsHost is false && nextDaemonAddresses.Any(IPAddress.IsLoopback))
            {
                return CreateInternalPoint();
            }

            Logger.LogInformation("Will create a point at {Daemon}", nextDaemon.HostUrl);

            if (_pointCreationService != null)
            {
                var point = await _pointCreationService.CreatePointAsync(
                    _jobId,
                    _moduleId,
                    _argumentsProvider.GetArguments(),
                    nextDaemon.HostUrl,
                    nextDaemon.Port,
                    _cancellationToken);

                _createdPoints.Add((Point)point);

                return point;
            }

            return await CreateNetworkPointAsync(nextDaemonAddresses, nextDaemon.Port);
        }

        private async Task<IPoint> CreateNetworkPointAsync(IPAddress[] nextDaemonAddresses, int daemonPort)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(nextDaemonAddresses, daemonPort);

            var networkChannel = new NetworkChannel(tcpClient);
            networkChannel.SetCancellation(_cancellationToken);

            var networkPoint = new Point(_jobId, _moduleId, networkChannel, _argumentsProvider);
            _createdPoints.Add(networkPoint);

            return networkPoint;
        }

        private IPoint CreateInternalPoint()
        {
            var internalChannelId = _internalChannelManager.Create();

            _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannelPair);
            internalChannelPair.Item1.SetCancellation(_cancellationToken);

            var internalPoint = new Point(_jobId, _moduleId, internalChannelPair.Item1, _argumentsProvider);
            _createdPoints.Add(internalPoint);

            return internalPoint;
        }

        private Daemon GetNextDaemon()
        {
            var availableDaemons = _daemonResolver.GetAvailableDaemons();

            Logger.LogInformation(
                "Available daemons: {Daemons}",
                string.Join(", ", availableDaemons.Select(d => d.HostUrl).ToArray()));

            Logger.LogInformation(
                "Current points on daemons: {PointsOnDaemons}",
                string.Join(", ", _pointsOnDaemons.Select(p => $"{p.Key}:{p.Value}").ToArray()));

            if (availableDaemons is null || !availableDaemons.Any())
            {
                throw new InvalidOperationException("No daemons available.");
            }

            foreach (var daemon in availableDaemons.Where(daemon => !_pointsOnDaemons.ContainsKey(daemon.HostUrl)))
            {
                Logger.LogInformation("Adding new daemon {HostUrl} to the dictionary", daemon.HostUrl);
                _pointsOnDaemons.TryAdd(daemon.HostUrl, 0);
            }

            var leastPointsDaemon = _pointsOnDaemons.FirstOrDefault(d => d.Value == _pointsOnDaemons.Min(d => d.Value));

            Logger.LogInformation("Least points daemon is {HostUrl} to the dictionary", leastPointsDaemon.Key);
            _pointsOnDaemons[leastPointsDaemon.Key]++;

            return availableDaemons.FirstOrDefault(d => d.HostUrl == leastPointsDaemon.Key);
        }

        public T BindModuleOptions<T>() where T : class, IModuleOptions, new() => _argumentsProvider.Bind<T>();

        public async ValueTask DisposeAsync()
        {
            if (_createdPoints.Count == 0)
            {
                return;
            }

            foreach (var point in _createdPoints)
            {
                await point.DisposeAsync();
            }

            _pointsOnDaemons.Clear();
            _createdPoints.Clear();
        }
    }
}