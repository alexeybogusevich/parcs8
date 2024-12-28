using Microsoft.Extensions.Logging;
using Parcs.Core.Models.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Services
{
    public class ChannelOrchestrator(ISignalHandlerFactory signalHandlerFactory, ILogger<ChannelOrchestrator> logger) : IChannelOrchestrator
    {
        private readonly ISignalHandlerFactory _signalHandlerFactory = signalHandlerFactory;
        private readonly ILogger<ChannelOrchestrator> _logger = logger;

        public async Task OrchestrateAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            managedChannel.SetCancellation(cancellationToken);

            try
            {
                while (true)
                {
                    var signal = await managedChannel.ReadSignalAsync();

                    if (signal == Signal.CloseConnection)
                    {
                        return;
                    }

                    await _signalHandlerFactory.Create(signal).HandleAsync(managedChannel, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception thrown: {Message}.", ex.Message);
            }
            finally
            {
                managedChannel.Dispose();
            }
        }
    }
}