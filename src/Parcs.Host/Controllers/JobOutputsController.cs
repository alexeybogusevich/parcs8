using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Host.Models.Queries;
using System.Net;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobOutputsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpGet("{JobId}")]
        [ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetAsync([FromRoute] GetJobOutputQuery query, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(query, cancellationToken);

            if (response is null)
            {
                return NotFound();
            }

            return File(response.ArchivedOutput.Content, response.ArchivedOutput.ContentType, response.ArchivedOutput.Filename);
        }
    }
}