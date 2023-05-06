using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using System.Net;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModulesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ModulesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GetModuleQueryResponse>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(new GetAllModulesQuery(), cancellationToken);
            return Ok(response);
        }

        [HttpGet("{Id}")]
        [ProducesResponseType(typeof(GetModuleQueryResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetAsync([FromRoute] GetModuleQuery query, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(query, cancellationToken);

            if (response is null)
            {
                return NotFound();
            }

            return Ok(response);
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateModuleCommandResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateAsync([FromForm] CreateModuleCommand command, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }

        [HttpDelete]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        public async Task<IActionResult> DeleteAsync(CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteAllModulesCommand(), cancellationToken);
            return NoContent();
        }
    }
}