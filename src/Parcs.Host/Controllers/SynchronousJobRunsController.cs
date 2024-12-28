using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Host.Models.Commands;
using System.Net;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SynchronousJobRunsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost]
        [ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RunAsync([FromBody] RunJobSynchronouslyCommand command, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }
    }
}