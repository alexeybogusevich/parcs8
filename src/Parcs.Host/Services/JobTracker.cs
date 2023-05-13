using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Host.Services.Interfaces;
using Parcs.Net;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Parcs.Host.Services
{
    public sealed class JobTracker : IJobTracker
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _trackedJobs = new();

        public JobTracker(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public void StartTracking(long jobId)
        {
            _ = _trackedJobs.TryAdd(jobId, new CancellationTokenSource());
        }

        public bool TryGetCancellationToken(long jobId, out CancellationToken cancellationToken)
        {
            if (_trackedJobs.TryGetValue(jobId, out var cancellationTokenSource))
            {
                cancellationToken = cancellationTokenSource.Token;

                return true;
            }

            cancellationToken = CancellationToken.None;

            return false;
        }

        public async Task<bool> CancelAndStopTrackingAsync(long jobId)
        {
            if (!_trackedJobs.TryGetValue(jobId, out var cancellationTokenSource))
            {
                return false;
            }

            using var serviceScope = _serviceScopeFactory.CreateScope();

            var daemonCancellationTasks = serviceScope.ServiceProvider
                .GetRequiredService<IDaemonResolver>()
                .GetAvailableDaemons()
                .Select(
                    d => CancelOnDaemonAsync(d, jobId));

            await Task.WhenAll(daemonCancellationTasks);

            cancellationTokenSource.Cancel();

            return StopTracking(jobId);
        }

        private static async Task CancelOnDaemonAsync(Daemon daemon, long jobId)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(daemon.HostUrl, daemon.Port);

            using var networkChannel = new NetworkChannel(tcpClient);
            await networkChannel.WriteSignalAsync(Signal.CancelJob);
            await networkChannel.WriteDataAsync(jobId);

            await networkChannel.WriteSignalAsync(Signal.CloseConnection);
        }

        public bool StopTracking(long jobId) => _trackedJobs.TryRemove(jobId, out _);
    }
}