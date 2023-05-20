using Microsoft.AspNetCore.Mvc;

namespace Parcs.Portal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompletedJobsController : ControllerBase
    {
        [HttpPost("jobId")]
        public Task CreateAsync([FromRoute] long jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}