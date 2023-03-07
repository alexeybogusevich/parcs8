using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Domain;

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
        public async Task<IActionResult> CreateAsync([FromForm] CreateJobCommand command)
        {
            command.Daemons = new List<Daemon>
            {
                new Daemon
                {
                    IpAddress = "127.0.0.1",
                    Port = 1111,
                },
            };

            var response = await _mediator.Send(command);
            return Ok(response);
        }
    }
}