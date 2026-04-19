using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parcs.Core.Models;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>
        /// Streams job status as Server-Sent Events until the job reaches a terminal state.
        ///
        /// Each event is a JSON object: { "jobId": long, "status": string, "failures": string[] }
        /// Heartbeat comments (": heartbeat") are emitted every ~3 s to keep proxies and
        /// load-balancers from closing the idle connection.
        ///
        /// Terminal statuses that close the stream: Completed | Failed | Cancelled
        /// </summary>
        [HttpGet("{jobId}/stream")]
        public async Task StreamStatusAsync(long jobId, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache");
            // Disable nginx proxy buffering — without this, GKE's ingress will buffer SSE
            // events and the client won't receive them until the buffer fills.
            Response.Headers.Append("X-Accel-Buffering", "no");

            await Response.Body.FlushAsync(cancellationToken);

            var terminalStatuses = new[] { JobStatus.Completed, JobStatus.Failed, JobStatus.Cancelled };
            JobStatus? lastReportedStatus = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                var job = await _mediator.Send(new GetJobQuery(jobId), cancellationToken);

                if (job is null)
                {
                    await WriteSseAsync($"{{\"error\":\"Job {jobId} not found\"}}", cancellationToken);
                    return;
                }

                var currentStatus = job.Statuses
                    .OrderByDescending(s => s.CreateDateUtc)
                    .FirstOrDefault()?.Status ?? JobStatus.Unknown;

                if (currentStatus != lastReportedStatus)
                {
                    lastReportedStatus = currentStatus;

                    var failures = job.Failures
                        .OrderByDescending(f => f.CreateDateUtc)
                        .Select(f => f.Message)
                        .ToArray();

                    var payload = JsonSerializer.Serialize(new
                    {
                        jobId,
                        status   = currentStatus.ToString(),
                        failures,
                    });

                    await WriteSseAsync(payload, cancellationToken);
                }
                else
                {
                    // Heartbeat comment — not parsed as an event, just keeps the connection alive.
                    await Response.WriteAsync(": heartbeat\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                if (terminalStatuses.Contains(currentStatus))
                    return;

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        private async Task WriteSseAsync(string data, CancellationToken ct)
        {
            await Response.WriteAsync($"data: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GetPlainJobQueryResponse>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(new GetAllJobsQuery(), cancellationToken);
            return Ok(response);
        }

        [HttpGet("{JobId}")]
        [ProducesResponseType(typeof(GetJobQueryResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetAsync([FromRoute] GetJobQuery query, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(query, cancellationToken);

            if (response is null)
            {
                return NotFound();
            }

            return Ok(response);
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateJobCommandResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateAsync([FromForm] CreateJobCommand command, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }

        [HttpPost("{JobId}")]
        [ProducesResponseType(typeof(CloneJobCommandResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CloneAsync([FromRoute] CloneJobCommand command, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }

        [HttpPut("{JobId}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        public async Task<IActionResult> CancelAsync([FromRoute] CancelJobCommand command, CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }

        [HttpDelete("{JobId}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        public async Task<IActionResult> DeleteAsync([FromRoute] DeleteJobCommand command, CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
    }
}