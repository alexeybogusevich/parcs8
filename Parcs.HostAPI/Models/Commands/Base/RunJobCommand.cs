﻿using Parcs.Shared;

namespace Parcs.HostAPI.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(Guid jobId, IEnumerable<Daemon> daemons)
        {
            JobId = jobId;
            Daemons = daemons;
        }

        public Guid JobId { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}