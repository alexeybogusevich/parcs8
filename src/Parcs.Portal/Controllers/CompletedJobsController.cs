using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Parcs.Portal.Constants;
using Parcs.Portal.Hubs;
using System.Net;

namespace Parcs.Portal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompletedJobsController(IHubContext<JobCompletionHub> hubContext) : ControllerBase
    {
        private readonly IHubContext<JobCompletionHub> _hubContext = hubContext;

        [HttpPost("{jobId}")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        public async Task<IActionResult> CreateAsync([FromRoute] long jobId, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.All.SendAsync(JobCompletionHubMethods.NotifyCompletion, jobId, cancellationToken);
            return Accepted();
        }
    }
}