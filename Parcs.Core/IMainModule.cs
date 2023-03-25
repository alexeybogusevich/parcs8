﻿namespace Parcs.Net
{
    public interface IMainModule
    {
        Task RunAsync(IHostInfo hostInfo, IInputReader inputReader, IOutputWriter outputWriter, CancellationToken cancellationToken = default);
    }
}