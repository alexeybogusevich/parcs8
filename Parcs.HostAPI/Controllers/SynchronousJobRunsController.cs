using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Commands.Base;

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
        public async Task<IActionResult> RunAsync([FromForm] CreateSynchronousJobRunCommand command, CancellationToken cancellationToken)
        {
            var createJobCommand = new CreateJobCommand(command);
            var createJobCommandResponse = await _mediator.Send(createJobCommand, cancellationToken);

            var runJobCommand = new RunJobCommand(createJobCommandResponse.JobId, command.Daemons);
            var runJobSynchronouslyCommand = new RunJobSynchronouslyCommand(runJobCommand);
            var runJobSynchronouslyCommandResponse = await _mediator.Send(runJobSynchronouslyCommand, CancellationToken.None);

            var deleteJobCommand = new DeleteJobCommand(createJobCommandResponse.JobId);
            await _mediator.Send(deleteJobCommand, CancellationToken.None);

            return Ok(runJobSynchronouslyCommandResponse);
        }
    }
}