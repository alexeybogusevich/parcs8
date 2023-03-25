﻿using Parcs.Shared;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonSelector
    {
        IEnumerable<Daemon> Select(IEnumerable<Daemon> suppliedDaemons = null);
    }
}