using Parcs.Net;
using Parcs.Shared.Models.Enums;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public sealed class InputOutputFactory : IInputOutputFactory
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;

        public InputOutputFactory(IJobDirectoryPathBuilder jobDirectoryPathBuilder)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        }

        public IInputReader CreateReader(Guid jobId) =>
            new InputReader(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Input));

        public IOutputWriter CreateWriter(Guid jobId, CancellationToken cancellationToken) =>
            new OutputWriter(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Output), cancellationToken);
    }
}