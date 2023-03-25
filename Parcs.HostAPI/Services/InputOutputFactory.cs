﻿using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;

namespace Parcs.HostAPI.Services
{
    public sealed class InputOutputFactory : IInputOutputFactory
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;

        public InputOutputFactory(IJobDirectoryPathBuilder jobDirectoryPathBuilder)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        }

        public IInputReader CreateReader(Guid jobId) => new InputReader(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Input));

        public IOutputWriter CreateWriter(Guid jobId) => new OutputWriter(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Output));
    }
}