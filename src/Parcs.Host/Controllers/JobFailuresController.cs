using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Host.Models.Commands;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobFailuresController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateJobFailureCommand command, CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }
    }
}