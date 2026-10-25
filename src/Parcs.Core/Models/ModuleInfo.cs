using Parcs.Net;
using Parcs.Core.Services;
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
        IPointCreationService pointCreationService = null,
        IDaemonAllocator daemonAllocator = null,
        IPeerChannelService peerChannelService = null) : IModuleInfo
    {
        private readonly long _jobId = jobMetadata.JobId;
        private readonly long _moduleId = jobMetadata.ModuleId;

        private readonly List<Point> _createdPoints = [];
        private readonly List<DaemonLease> _daemonLeases = [];
        private readonly List<IChannel> _peerChannels = [];
        private readonly List<IPeerEndpoint> _peerEndpoints = [];
        private readonly CancellationToken _cancellationToken = cancellationToken;

        private readonly IDaemonResolver _daemonResolver = daemonResolver;
        private readonly IInternalChannelManager _internalChannelManager = internalChannelManager;
        private readonly IAddressResolver _addressResolver = addressResolver;
        private readonly IArgumentsProvider _argumentsProvider = argumentsProvider;
        private readonly IPointCreationService _pointCreationService = pointCreationService;
        // Without a shared allocator, points are balanced over the pool within this job only.
        private readonly IDaemonAllocator _daemonAllocator = daemonAllocator ?? new DaemonAllocator();
        private readonly IPeerChannelService _peerChannelService = peerChannelService;

        public bool IsHost => Parent is null;

        public IChannel Parent { get; } = parentChannel;

        public IInputReader InputReader { get; } = inputOutputFactory.CreateReader(jobMetadata.JobId);

        public IOutputWriter OutputWriter { get; } = inputOutputFactory.CreateWriter(jobMetadata.JobId, cancellationToken);

        public ILogger Logger { get; } = logger;

        public async Task<IPoint> CreatePointAsync()
        {
            // Queue / KEDA path: point creation service handles daemon provisioning, on the Host
            // and on daemons alike (nested points). Checked before GetNextDaemon() because in
            // KEDA mode no daemons are pre-registered — pods are provisioned per point request.
            if (_pointCreationService is { IsEnabled: true })
            {
                var point = await _pointCreationService.CreatePointAsync(
                    _jobId,
                    _moduleId,
                    _argumentsProvider.GetArguments(),
                    _cancellationToken);

                _createdPoints.Add((Point)point);
                return point;
            }

            // Legacy path: resolve a pre-registered daemon and connect directly.
            var nextDaemon = GetNextDaemon();
            Logger.LogInformation("Resolved daemon {Daemon}", nextDaemon.HostUrl);

            var nextDaemonAddresses = _addressResolver.Resolve(nextDaemon.HostUrl);
            Logger.LogInformation("Resolved daemon address {Daemon}", nextDaemonAddresses.ToString());

            if (IsHost is false && nextDaemonAddresses.Any(IPAddress.IsLoopback))
            {
                return CreateInternalPoint();
            }

            Logger.LogInformation("Will create a point at {Daemon}", nextDaemon.HostUrl);
            return await CreateNetworkPointAsync(nextDaemonAddresses, nextDaemon.Port);
        }

        /// <inheritdoc/>
        public async Task<IPoint[]> CreatePointsAsync(int count)
        {
            if (_pointCreationService is { IsEnabled: true })
            {
                var points = await _pointCreationService.CreatePointsAsync(
                    count,
                    _jobId,
                    _moduleId,
                    _argumentsProvider.GetArguments(),
                    _cancellationToken);

                foreach (var p in points)
                {
                    _createdPoints.Add((Point)p);
                }

                return points;
            }

            // Fallback for legacy paths (no point queue configured).
            var result = new IPoint[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = await CreatePointAsync();
            }
            return result;
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
            var availableDaemons = _daemonResolver.GetAvailableDaemons()?.ToList();

            Logger.LogInformation(
                "Available daemons: {Daemons}",
                string.Join(", ", (availableDaemons ?? []).Select(d => d.HostUrl)));

            if (availableDaemons is null || availableDaemons.Count == 0)
            {
                throw new InvalidOperationException("No daemons available.");
            }

            // The lease is held until this module's points are disposed (end of the job).
            var lease = _daemonAllocator.Acquire(availableDaemons);
            _daemonLeases.Add(lease);

            Logger.LogInformation(
                "Placed point on {HostUrl}; active points per daemon: {ActivePoints}",
                lease.Daemon.HostUrl,
                string.Join(", ", _daemonAllocator.ActivePoints.Select(p => $"{p.Key}={p.Value}")));

            return lease.Daemon;
        }

        public Task<IPeerEndpoint> OpenEndpointAsync()
        {
            var endpoint = PeerChannels.OpenEndpoint(_cancellationToken);
            lock (_peerEndpoints)
            {
                _peerEndpoints.Add(endpoint);
            }

            return Task.FromResult(new TrackedPeerEndpoint(endpoint, TrackPeerChannel) as IPeerEndpoint);
        }

        public async Task<IChannel> ConnectAsync(string endpointAddress)
        {
            var channel = await PeerChannels.ConnectAsync(endpointAddress, _cancellationToken);
            TrackPeerChannel(channel);
            return channel;
        }

        private IPeerChannelService PeerChannels => _peerChannelService
            ?? throw new NotSupportedException("Peer channels are not available in this process (no callback server).");

        private void TrackPeerChannel(IChannel channel)
        {
            lock (_peerChannels)
            {
                _peerChannels.Add(channel);
            }
        }

        public T BindModuleOptions<T>() where T : class, IModuleOptions, new() => _argumentsProvider.Bind<T>();

        public async ValueTask DisposeAsync()
        {
            await DisposePeersAsync();

            if (_createdPoints.Count == 0)
            {
                ReleaseDaemonLeases();
                return;
            }

            foreach (var point in _createdPoints)
            {
                await point.DisposeAsync();
            }

            ReleaseDaemonLeases();
            _createdPoints.Clear();
        }

        private async Task DisposePeersAsync()
        {
            IPeerEndpoint[] endpoints;
            IChannel[] channels;
            lock (_peerEndpoints)
            {
                endpoints = [.. _peerEndpoints];
                _peerEndpoints.Clear();
            }
            lock (_peerChannels)
            {
                channels = [.. _peerChannels];
                _peerChannels.Clear();
            }

            foreach (var endpoint in endpoints)
            {
                await endpoint.DisposeAsync();
            }
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }

        /// <summary>Hands the accepted channel to the module info, which closes it with the module.</summary>
        private sealed class TrackedPeerEndpoint(IPeerEndpoint inner, Action<IChannel> track) : IPeerEndpoint
        {
            public string Address => inner.Address;

            public async Task<IChannel> AcceptAsync(CancellationToken cancellationToken = default)
            {
                var channel = await inner.AcceptAsync(cancellationToken);
                track(channel);
                return channel;
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }

        private void ReleaseDaemonLeases()
        {
            foreach (var lease in _daemonLeases)
            {
                lease.Dispose();
            }

            _daemonLeases.Clear();
        }
    }
}
