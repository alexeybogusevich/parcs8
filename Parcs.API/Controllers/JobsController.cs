using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public JobsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> RunAsync([FromForm] RunJobCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [HttpDelete("{JobId}")]
        public async Task<IActionResult> AbortAsync([FromRoute] AbortJobCommand command)
        {
            var wasAborted = await _mediator.Send(command);

            if (!wasAborted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}