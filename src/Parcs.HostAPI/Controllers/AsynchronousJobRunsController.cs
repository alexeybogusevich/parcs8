using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;
using System.Net;

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
        [ProducesResponseType(typeof(CreateAsynchronousJobRunCommandResponse), (int)HttpStatusCode.Accepted)]
        public async Task<IActionResult> ScheduleAsync([FromForm] CreateAsynchronousJobRunCommand command, CancellationToken cancellationToken)
        {
            var createJobCommand = new CreateJobCommand(command);
            var createJobCommandResponse = await _mediator.Send(createJobCommand, cancellationToken);

            var runJobCommand = new RunJobCommand(createJobCommandResponse.JobId, command.JsonArgumentsDictionary, command.NumberOfDaemons);
            var runJobAsynchronouslyCommand = new RunJobAsynchronouslyCommand(runJobCommand, command.CallbackUri);
            await _mediator.Send(runJobAsynchronouslyCommand, cancellationToken);

            var response = new CreateAsynchronousJobRunCommandResponse(createJobCommandResponse.JobId);

            return Accepted(response);
        }
    }
}