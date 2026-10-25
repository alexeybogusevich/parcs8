using Microsoft.Extensions.Logging;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    /// <summary>
    /// OpenLink (job id, name): opens a peer endpoint for one link of this point and answers with
    /// its address; the first connection to it becomes the link. The reply is (true, address) or
    /// (false, reason).
    /// </summary>
    public sealed class OpenLinkSignalHandler(
        IJobContextAccessor jobContextAccessor,
        IPeerChannelService peerChannelService,
        PointLinkRegistry linkRegistry,
        ILogger<OpenLinkSignalHandler> logger) : ISignalHandler
    {
        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();
            var name = await managedChannel.ReadStringAsync();

            if (!jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                await LinkReplies.FailAsync(managedChannel, $"job {jobId} is not initialized on this daemon");
                return;
            }

            var endpoint = peerChannelService.OpenEndpoint(jobContext.CancellationTokenSource.Token);
            if (!linkRegistry.For(managedChannel).TryAdd(name, AcceptOneAsync(endpoint, jobContext.CancellationTokenSource.Token)))
            {
                await endpoint.DisposeAsync();
                await LinkReplies.FailAsync(managedChannel, $"the point already has a link named '{name}'");
                return;
            }

            logger.LogInformation("Job {JobId}: link '{Name}' listening at {Address}", jobId, name, endpoint.Address);
            await LinkReplies.SucceedAsync(managedChannel, endpoint.Address);
        }

        private static async Task<IChannel> AcceptOneAsync(IPeerEndpoint endpoint, CancellationToken cancellationToken)
        {
            await using (endpoint)
            {
                return await endpoint.AcceptAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// ConnectLink (job id, name, address): dials the other end of a link from this point. The
    /// dial runs in the background, so the module starts at once and waits for the link only
    /// when it asks for it. The reply is (true, "") or (false, reason).
    /// </summary>
    public sealed class ConnectLinkSignalHandler(
        IJobContextAccessor jobContextAccessor,
        IPeerChannelService peerChannelService,
        PointLinkRegistry linkRegistry,
        ILogger<ConnectLinkSignalHandler> logger) : ISignalHandler
    {
        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();
            var name = await managedChannel.ReadStringAsync();
            var address = await managedChannel.ReadStringAsync();

            if (!jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                await LinkReplies.FailAsync(managedChannel, $"job {jobId} is not initialized on this daemon");
                return;
            }

            if (!linkRegistry.For(managedChannel).TryAdd(name, peerChannelService.ConnectAsync(address, jobContext.CancellationTokenSource.Token)))
            {
                await LinkReplies.FailAsync(managedChannel, $"the point already has a link named '{name}'");
                return;
            }

            logger.LogInformation("Job {JobId}: link '{Name}' dialing {Address}", jobId, name, address);
            await LinkReplies.SucceedAsync(managedChannel, string.Empty);
        }
    }

    internal static class LinkReplies
    {
        public static async Task SucceedAsync(IManagedChannel channel, string value)
        {
            await channel.WriteDataAsync(true);
            await channel.WriteDataAsync(value);
        }

        public static async Task FailAsync(IManagedChannel channel, string reason)
        {
            await channel.WriteDataAsync(false);
            await channel.WriteDataAsync(reason);
        }
    }
}
