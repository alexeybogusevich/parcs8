using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Host.Models.Commands;
using System.Net;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AsynchronousJobRunsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        public async Task<IActionResult> ScheduleAsync([FromBody] RunJobAsynchronouslyCommand command, CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);
            return Accepted();
        }
    }
}