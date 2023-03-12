using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly IMainModule _mainModule;
        private readonly IJobManager _jobManager;
        private readonly IDaemonSelector _daemonPicker;

        public CreateJobCommandHandler(
            IHostInfoFactory hostInfoFactory, IMainModule mainModule, IJobManager jobManager, IDaemonSelector daemonPicker)
        {
            _hostInfoFactory = hostInfoFactory;
            _mainModule = mainModule;
            _jobManager = jobManager;
            _daemonPicker = daemonPicker;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create();

            var selectedDaemons = _daemonPicker.Select(request.Daemons);
            job.SetDaemons(selectedDaemons);

            var hostInfo = _hostInfoFactory.Create(selectedDaemons);

            try
            {
                job.Start();
                var moduleOutput = await _mainModule.RunAsync(hostInfo, job.CancellationToken);
                job.Finish(moduleOutput.Result);
            }
            catch (Exception ex)
            {
                job.Fail(ex.Message);
            }

            return new CreateJobCommandResponse
            {
                ElapsedSeconds = job.ExecutionTime?.TotalSeconds,
                JobStatus = job.Status,
                ErrorMessage = job.ErrorMessage,
                Result = job.Result,
            };
        }
    }
}