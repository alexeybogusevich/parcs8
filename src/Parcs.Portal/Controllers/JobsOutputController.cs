using Microsoft.AspNetCore.Mvc;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsOutputController(IHostClient hostClient) : ControllerBase
    {
        private readonly IHostClient _hostClient = hostClient;

        [HttpGet("{jobId}")]
        [Produces("text/plain")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<IActionResult> GetAsync([FromRoute] long jobId, CancellationToken cancellationToken)
        {
            var jobOutput = await _hostClient.GetJobOutputAsync(jobId, cancellationToken);

            if (jobOutput == null)
            {
                return NotFound($"Job output not found.");
            }

            return File(jobOutput.Content, "application/force-download", jobOutput.Filename);
        }
    }
}