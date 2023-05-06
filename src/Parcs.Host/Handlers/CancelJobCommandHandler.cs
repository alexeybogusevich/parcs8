using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Handlers
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