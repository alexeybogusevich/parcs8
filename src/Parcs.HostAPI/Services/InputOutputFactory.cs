using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class InputOutputFactory : IInputOutputFactory
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;

        public InputOutputFactory(IJobDirectoryPathBuilder jobDirectoryPathBuilder)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        }

        public IInputReader CreateReader(Job job) =>
            new InputReader(_jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Input));

        public IOutputWriter CreateWriter(Job job) =>
            new OutputWriter(_jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output), job.CancellationToken);
    }
}