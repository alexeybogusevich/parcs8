using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScheduledJobsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ScheduledJobsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> ScheduleAsync([FromBody] ScheduleJobCommand command)
        {
            await _mediator.Send(command);
            return Accepted();
        }
    }
}