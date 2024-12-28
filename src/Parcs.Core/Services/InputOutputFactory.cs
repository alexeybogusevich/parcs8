using Parcs.Net;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class InputOutputFactory(IJobDirectoryPathBuilder jobDirectoryPathBuilder) : IInputOutputFactory
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder = jobDirectoryPathBuilder;

        public IInputReader CreateReader(long jobId) =>
            new InputReader(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Input));

        public IOutputWriter CreateWriter(long jobId, CancellationToken cancellationToken) =>
            new OutputWriter(_jobDirectoryPathBuilder.Build(jobId, JobDirectoryGroup.Output), cancellationToken);
    }
}