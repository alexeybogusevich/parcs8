using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScheduledJobRunsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ScheduledJobRunsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> ScheduleAsync([FromBody] ScheduleJobRunCommand command)
        {
            await _mediator.Send(command);
            return Accepted();
        }
    }
}