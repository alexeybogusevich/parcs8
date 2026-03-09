using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Host.Services.Interfaces;
using Parcs.Net;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Parcs.Host.Services
{
    public sealed class JobTracker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<JobTracker> logger) : IJobTracker
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<JobTracker> _logger = logger;
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _trackedJobs = new();

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

            try
            {
                // In KEDA mode, daemon pods are ScaledJobs that exit when finished.
                // GetAvailableDaemons() does a DNS lookup on the headless service; if no
                // daemon pods are running (job completed normally) the lookup returns zero
                // addresses or throws SocketException — both are fine, nothing to cancel.
                var daemonCancellationTasks = serviceScope.ServiceProvider
                    .GetRequiredService<IDaemonResolver>()
                    .GetAvailableDaemons()
                    .Select(d => CancelOnDaemonAsync(d, jobId))
                    .ToList();

                if (daemonCancellationTasks.Count > 0)
                {
                    await Task.WhenAll(daemonCancellationTasks);
                }
            }
            catch (SocketException)
            {
                // Expected when no daemon pods are running — headless service has no endpoints.
                _logger.LogDebug("No active daemon pods found for job {JobId} cleanup (DNS returned no addresses).", jobId);
            }
            catch (Exception ex)
            {
                // Daemon cancellation is best-effort; log and continue cleanup.
                _logger.LogWarning(ex, "Daemon cancellation for job {JobId} failed: {Message}", jobId, ex.Message);
            }

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