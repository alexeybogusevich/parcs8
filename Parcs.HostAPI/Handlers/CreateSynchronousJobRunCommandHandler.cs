using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateSynchronousJobRunCommandHandler : IRequestHandler<CreateSynchronousJobRunCommand, CreateSynchronousJobRunCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IInputReaderFactory _inputReaderFactory;
        private readonly IMainModuleLoader _mainModuleLoader;
        private readonly IJobManager _jobManager;
        private readonly IDaemonSelector _daemonSelector;

        public CreateSynchronousJobRunCommandHandler(
            IHostInfoFactory hostInfoFactory,
            IInputReaderFactory inputReaderFactory,
            IMainModuleLoader mainModuleLoader,
            IJobManager jobManager,
            IDaemonSelector daemonSelector)
        {
            _hostInfoFactory = hostInfoFactory;
            _inputReaderFactory = inputReaderFactory;
            _mainModuleLoader = mainModuleLoader;
            _jobManager = jobManager;
            _daemonSelector = daemonSelector;
        }

        public async Task<CreateSynchronousJobRunCommandResponse> Handle(CreateSynchronousJobRunCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {request.JobId}");
            }

            var selectedDaemons = _daemonSelector.Select(request.Daemons);
            job.SetDaemons(selectedDaemons);

            var hostInfo = _hostInfoFactory.Create(selectedDaemons);
            var inputReader = _inputReaderFactory.Create(job.Id);

            try
            {
                var mainModule = job.MainModule ?? await _mainModuleLoader.LoadAsync(job.ModuleId, job.AssemblyName, job.ClassName, job.CancellationToken);
                
                job.Start();
                var moduleOutput = await mainModule.RunAsync(hostInfo, inputReader, job.CancellationToken);
                job.Finish(moduleOutput.Result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                job.Fail(ex.Message);
            }

            return new CreateSynchronousJobRunCommandResponse(job);
        }
    }
}