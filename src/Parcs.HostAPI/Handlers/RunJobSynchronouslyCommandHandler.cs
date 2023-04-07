using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class RunJobSynchronouslyCommandHandler : IRequestHandler<RunJobSynchronouslyCommand, RunJobSynchronouslyCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IJobManager _jobManager;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory;

        public RunJobSynchronouslyCommandHandler(
            IHostInfoFactory hostInfoFactory,
            IJobManager jobManager,
            IArgumentsProviderFactory argumentsProviderFactory)
        {
            _hostInfoFactory = hostInfoFactory;
            _jobManager = jobManager;
            _argumentsProviderFactory = argumentsProviderFactory;
        }

        public async Task<RunJobSynchronouslyCommandResponse> Handle(RunJobSynchronouslyCommand command, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(command.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {command.JobId}");
            }

            ArgumentNullException.ThrowIfNull(job.MainModule);

            var argumentsProvider = _argumentsProviderFactory.Create(command.JsonArgumentsDictionary);
            await using var hostInfo = await _hostInfoFactory.CreateAsync(job);

            try
            {
                job.Start();
                await job.MainModule.RunAsync(argumentsProvider, hostInfo, job.CancellationToken);
                job.Finish();
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                job.Fail(ex.Message);
            }

            return new RunJobSynchronouslyCommandResponse(job);
        }
    }
}