using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Queries;

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

        [HttpGet("{JobId}")]
        public async Task<IActionResult> GetAsync([FromRoute] GetJobQuery query, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> RunAsync([FromForm] CreateJobCommand command, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }
    }
}