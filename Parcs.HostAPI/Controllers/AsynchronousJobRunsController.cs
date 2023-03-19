using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Commands.Base;

namespace Parcs.HostAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AsynchronousJobRunsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AsynchronousJobRunsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> ScheduleAsync([FromForm] CreateAsynchronousJobRunCommand command, CancellationToken cancellationToken)
        {
            var createJobCommand = new CreateJobCommand(command);
            var createJobCommandResponse = await _mediator.Send(createJobCommand, cancellationToken);

            var runJobCommand = new RunJobCommand(createJobCommandResponse.JobId, command.Daemons);
            var runJobAsynchronouslyCommand = new RunJobAsynchronouslyCommand(runJobCommand, command.CallbackUrl);
            await _mediator.Send(runJobAsynchronouslyCommand, cancellationToken);

            return Accepted(createJobCommandResponse);
        }
    }
}