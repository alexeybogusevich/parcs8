using Parcs.Net;
using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using Parcs.Core.Models.Interfaces;

namespace Parcs.Core.Models
{
    public sealed class ModuleInfo : IModuleInfo
    {
        private readonly Guid _jobId;
        private readonly Guid _moduleId;
        private readonly CancellationToken _cancellationToken;

        private readonly List<Point> _createdPoints;
        private readonly Dictionary<string, int> _pointsOnDaemons;

        private readonly IChannel _parentChannel;
        private readonly IInputReader _inputReader;
        private readonly IOutputWriter _outputWriter;
        private readonly IArgumentsProvider _argumentsProvider;
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
            _createdPoints = new ();
            _pointsOnDaemons = new ();
            _parentChannel = parentChannel;
            _inputReader = inputOutputFactory.CreateReader(jobId);
            _outputWriter = inputOutputFactory.CreateWriter(jobId, cancellationToken);
            _argumentsProvider = argumentsProvider;
            _daemonResolver = daemonResolver;
            _internalChannelManager = internalChannelManager;
            _addressResolver = addressResolver;
            _cancellationToken = cancellationToken;
        }

        public IChannel Parent => _parentChannel;

        public IInputReader InputReader => _inputReader;

        public IOutputWriter OutputWriter => _outputWriter;

        public IArgumentsProvider ArgumentsProvider => _argumentsProvider;

        public async Task<IPoint> CreatePointAsync()
        {
            var nextDaemon = GetNextDaemon();

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(nextDaemon.HostUrl, nextDaemon.Port);

            IManagedChannel channel = new NetworkChannel(tcpClient);

            if (_addressResolver.IsSameAddressAsHost(nextDaemon.HostUrl))
            {
                var internalChannel = await InitializeInternalAsync(channel);
                channel.Dispose();
                channel = internalChannel;
            }

            channel.SetCancellation(_cancellationToken);

            var point = new Point(_jobId, _moduleId, channel, _argumentsProvider);
            _createdPoints.Add(point);

            return point;
        }

        private async Task<InternalChannel> InitializeInternalAsync(IManagedChannel channel)
        {
            var internalChannelId = _internalChannelManager.Create();

            await channel.WriteSignalAsync(Signal.InternalChannelSwitch);
            await channel.WriteDataAsync(internalChannelId);

            var signal = await channel.ReadSignalAsync();

            if (signal != Signal.InternalChannelSwitch)
            {
                throw new ArgumentException($"Protocol switch declined.");
            }

            _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannel);

            return internalChannel;
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