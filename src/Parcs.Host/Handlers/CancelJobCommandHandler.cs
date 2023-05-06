using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand>
    {
        private readonly IJobTracker _jobTracker;

        public CancelJobCommandHandler(IJobTracker jobTracker)
        {
            _jobTracker = jobTracker;
        }

        public Task Handle(CancelJobCommand request, CancellationToken cancellationToken)
        {
            _jobTracker.CancelAndStopTrackning(request.JobId);

            return Task.CompletedTask;
        }
    }
}