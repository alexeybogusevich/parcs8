using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Queries;
using System.Net;

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
        [ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RunAsync([FromForm] CreateSynchronousJobRunCommand command, CancellationToken cancellationToken)
        {
            var createJobCommand = new CreateJobCommand(command);
            var createJobCommandResponse = await _mediator.Send(createJobCommand, cancellationToken);

            var runJobCommand = new RunJobCommand(createJobCommandResponse.JobId, command.JsonArgumentsDictionary, command.NumberOfDaemons);
            var runJobSynchronouslyCommand = new RunJobSynchronouslyCommand(runJobCommand);
            _ = await _mediator.Send(runJobSynchronouslyCommand, CancellationToken.None);

            var getJobOutputQuery = new GetJobOutputQuery(createJobCommandResponse.JobId);
            var getJobOutputQueryResponse = await _mediator.Send(getJobOutputQuery, CancellationToken.None);
            var jobOutput = getJobOutputQueryResponse.ArchivedOutput;

            var deleteJobCommand = new DeleteJobCommand(createJobCommandResponse.JobId);
            await _mediator.Send(deleteJobCommand, CancellationToken.None);

            return File(jobOutput.Content, jobOutput.ContentType, jobOutput.Filename);
        }
    }
}