﻿using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Shared.Models.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public sealed class DefaultSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}