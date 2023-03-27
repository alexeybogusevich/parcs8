using Microsoft.Extensions.Logging;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    public sealed class DefaultSignalHandler : ISignalHandler
    {
        private readonly ILogger<DefaultSignalHandler> _logger;

        public DefaultSignalHandler(ILogger<DefaultSignalHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Unexpected signal received.");
            return Task.CompletedTask;
        }
    }
}