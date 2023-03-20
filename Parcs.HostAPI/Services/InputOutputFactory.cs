using Parcs.Core;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class InputOutputFactory : IInputOutputFactory
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