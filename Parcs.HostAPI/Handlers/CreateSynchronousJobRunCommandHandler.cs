using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateSynchronousJobRunCommandHandler : IRequestHandler<CreateSynchronousJobRunCommand, CreateSynchronousJobRunCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IInputReaderFactory _inputReaderFactory;
        private readonly IMainModule _mainModule;
        private readonly IJobManager _jobManager;
        private readonly IDaemonSelector _daemonSelector;

        public CreateSynchronousJobRunCommandHandler(
            IHostInfoFactory hostInfoFactory,
            IInputReaderFactory inputReaderFactory,
            IMainModule mainModule,
            IJobManager jobManager,
            IDaemonSelector daemonSelector)
        {
            _hostInfoFactory = hostInfoFactory;
            _inputReaderFactory = inputReaderFactory;
            _mainModule = mainModule;
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
                job.Start();
                var moduleOutput = await _mainModule.RunAsync(hostInfo, inputReader, job.CancellationToken);
                job.Finish(moduleOutput.Result);
            }
            catch (Exception ex)
            {
                job.Fail(ex.Message);
            }

            return new CreateSynchronousJobRunCommandResponse(job);
        }
    }
}