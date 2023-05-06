using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;
using Parcs.Net;
using System.Net.Sockets;

namespace Parcs.Host.Handlers
{
    public class CreateJobFailureCommandHandler : IRequestHandler<CreateJobFailureCommand>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IDaemonResolver _daemonResolver;
        private readonly IJobTracker _jobTracker;

        public CreateJobFailureCommandHandler(ParcsDbContext parcsDbContext, IDaemonResolver daemonResolver, IJobTracker jobTracker)
        {
            _parcsDbContext = parcsDbContext;
            _daemonResolver = daemonResolver;
            _jobTracker = jobTracker;
        }

        public async Task Handle(CreateJobFailureCommand request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs
                .Include(e => e.Statuses)
                .Include(e => e.Failures)
                .FirstOrDefaultAsync(e => e.Id == request.JobId, CancellationToken.None) ?? throw new ArgumentException("Job not found.");

            await _parcsDbContext.JobFailures.AddAsync(new(job.Id, request.Message, request.StackTrace), CancellationToken.None);
            await _parcsDbContext.SaveChangesAsync(CancellationToken.None);

            await Task.WhenAll(_daemonResolver.GetAvailableDaemons().Select(d => CancelJobAsync(job.Id, d)));

            _jobTracker.CancelAndStopTrackning(job.Id);
        }

        private static async Task CancelJobAsync(long jobId, Daemon daemon)
        {
            var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(daemon.HostUrl, daemon.Port);

            var networkChannel = new NetworkChannel(tcpClient);

            await networkChannel.WriteSignalAsync(Signal.CancelJob);
            await networkChannel.WriteDataAsync(jobId);
        }
    }
}