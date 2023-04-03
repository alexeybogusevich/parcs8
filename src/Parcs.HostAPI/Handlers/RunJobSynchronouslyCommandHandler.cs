using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class RunJobSynchronouslyCommandHandler : IRequestHandler<RunJobSynchronouslyCommand, RunJobSynchronouslyCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IMainModuleLoader _mainModuleLoader;
        private readonly IJobManager _jobManager;
        private readonly IDaemonSelector _daemonSelector;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory;

        public RunJobSynchronouslyCommandHandler(
            IHostInfoFactory hostInfoFactory,
            IMainModuleLoader mainModuleLoader,
            IJobManager jobManager,
            IDaemonSelector daemonSelector,
            IArgumentsProviderFactory argumentsProviderFactory)
        {
            _hostInfoFactory = hostInfoFactory;
            _mainModuleLoader = mainModuleLoader;
            _jobManager = jobManager;
            _daemonSelector = daemonSelector;
            _argumentsProviderFactory = argumentsProviderFactory;
        }

        public async Task<RunJobSynchronouslyCommandResponse> Handle(RunJobSynchronouslyCommand command, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(command.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {command.JobId}");
            }

            var argumentsProvider = _argumentsProviderFactory.Create(command.JsonArgumentsDictionary);
            var availableDaemons = _daemonSelector.Select(command.NumberOfDaemons);
            await using var hostInfo = _hostInfoFactory.Create(job, availableDaemons);

            try
            {
                var mainModule = job.MainModule ?? _mainModuleLoader.Load(job.ModuleId, job.AssemblyName, job.ClassName);

                job.Start();
                await mainModule.RunAsync(argumentsProvider, hostInfo, job.CancellationToken);
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