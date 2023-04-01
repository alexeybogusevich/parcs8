using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class RunJobSynchronouslyCommandHandler : IRequestHandler<RunJobSynchronouslyCommand, RunJobSynchronouslyCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IInputOutputFactory _inputOutputFactory;
        private readonly IMainModuleLoader _mainModuleLoader;
        private readonly IJobManager _jobManager;
        private readonly IDaemonSelector _daemonSelector;

        public RunJobSynchronouslyCommandHandler(
            IHostInfoFactory hostInfoFactory,
            IInputOutputFactory inputOutputFactory,
            IMainModuleLoader mainModuleLoader,
            IJobManager jobManager,
            IDaemonSelector daemonSelector)
        {
            _hostInfoFactory = hostInfoFactory;
            _inputOutputFactory = inputOutputFactory;
            _mainModuleLoader = mainModuleLoader;
            _jobManager = jobManager;
            _daemonSelector = daemonSelector;
        }

        public async Task<RunJobSynchronouslyCommandResponse> Handle(RunJobSynchronouslyCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {request.JobId}");
            }

            var availableDaemons = _daemonSelector.Select(request.Daemons);
            
            var hostInfo = _hostInfoFactory.Create(job, availableDaemons);
            var inputReader = _inputOutputFactory.CreateReader(job);
            var outputWriter = _inputOutputFactory.CreateWriter(job);

            try
            {
                var mainModule = job.MainModule ?? _mainModuleLoader.Load(job.ModuleId, job.AssemblyName, job.ClassName);

                job.Start();
                await mainModule.RunAsync(hostInfo, inputReader, outputWriter);
                job.Finish();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                job.Fail(ex.Message);
            }

            return new RunJobSynchronouslyCommandResponse(job);
        }
    }
}