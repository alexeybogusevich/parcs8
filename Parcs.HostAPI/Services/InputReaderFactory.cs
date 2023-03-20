using Parcs.Core;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class InputReaderFactory : IInputReaderFactory
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;

        public InputReaderFactory(IJobDirectoryPathBuilder jobDirectoryPathBuilder)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        }

        public IInputReader Create(Guid jobId) => new InputReader(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Input));
    }
}