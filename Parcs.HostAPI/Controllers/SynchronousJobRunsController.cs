using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SynchronousJobRunsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SynchronousJobRunsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> RunAsync([FromBody] CreateSynchronousJobRunCommand command, CancellationToken cancellationToken)
        {
            var createJobCommand = new CreateJobCommand(command);
            _ = await _mediator.Send(createJobCommand, cancellationToken);

            var runJobCommand = new RunJobSynchronouslyCommand { Daemons = command.Daemons, JobId = command.JobId };
            var runJobCommandResponse = await _mediator.Send(runJobCommand, cancellationToken);

            return Ok(runJobCommandResponse);
        }
    }
}