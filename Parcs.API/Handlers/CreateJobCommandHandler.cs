using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IHostInfoFactory _hostInfoFactory;
        private readonly ILogger<CreateJobCommandHandler> _logger;

        public CreateJobCommandHandler(IHostInfoFactory hostInfoFactory, ILogger<CreateJobCommandHandler> logger)
        {
            _hostInfoFactory = hostInfoFactory;
            _logger = logger;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var hostInfo = _hostInfoFactory.Create(request.Daemons);

            _logger.LogInformation("Daemons for the request (Module = {ModuleId}):", request.ModuleId);
            foreach (var daemon in request.Daemons)
            {
                _logger.LogInformation("IP address: {IpAddress}, Port: {Port}.", daemon.IpAddress, daemon.Port);
            }

            var pointsNumber = hostInfo.MaximumPointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = points[i].CreateChannel();
                await channels[i].ExecuteClassAsync("Some funny class :)");
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(10.1D, cancellationToken);
                await channels[i].WriteDataAsync(true, cancellationToken);
                await channels[i].WriteDataAsync("Hello world", cancellationToken);
                await channels[i].WriteDataAsync((byte)1, cancellationToken);
                await channels[i].WriteDataAsync(123L, cancellationToken);
                await channels[i].WriteDataAsync(22, cancellationToken);

                var job = new Job
                {
                    StartDateUtc = DateTime.UtcNow,
                    Status = JobStatus.InProgress,
                    CreateDateUtc = DateTime.UtcNow.AddDays(-1),
                    EndDateUtc = DateTime.UtcNow.AddDays(1),
                    Id = Guid.NewGuid(),
                };

                await channels[i].WriteObjectAsync(job, cancellationToken);
            }
            DateTime time = DateTime.Now;
            _logger.LogInformation("Waiting for result...");

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync(cancellationToken);
            }
            var elapsedSeconds = Math.Round((DateTime.Now - time).TotalSeconds, 3);

            _logger.LogInformation("Result found: res = {result}, time = {elapsedTime}", result, elapsedSeconds);

            _logger.LogInformation("Disconnecting the client...");

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i].Delete();
            }

            _logger.LogInformation("Done!");

            return new CreateJobCommandResponse
            {
                ElapsedSeconds = elapsedSeconds,
                JobStatus = JobStatus.Finished,
                Result = result,
            };
        }
    }
}