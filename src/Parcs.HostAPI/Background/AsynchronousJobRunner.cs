using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;
using System.Threading.Channels;

namespace Parcs.HostAPI.Background
{
    public class AsynchronousJobRunner : BackgroundService
    {
        private readonly IJobManager _jobManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ChannelReader<RunJobAsynchronouslyCommand> _channelReader;
        private readonly ILogger<AsynchronousJobRunner> _logger;

        public AsynchronousJobRunner(
            IJobManager jobManager,
            IServiceScopeFactory serviceScopeFactory,
            ChannelReader<RunJobAsynchronouslyCommand> channelReader,
            ILogger<AsynchronousJobRunner> logger)
        {
            _jobManager = jobManager;
            _serviceScopeFactory = serviceScopeFactory;
            _channelReader = channelReader;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var command in _channelReader.ReadAllAsync(stoppingToken))
            {
                await HandleCommandAsync(command, stoppingToken);
            }
        }

        private async Task HandleCommandAsync(RunJobAsynchronouslyCommand command, CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var jobCompletionNotifier = scope.ServiceProvider.GetRequiredService<IJobCompletionNotifier>();

            if (!_jobManager.TryGet(command.JobId, out var job))
            {
                throw new ArgumentException($"Job {job.Id} not found.");
            }

            try
            {
                var runJobCommand = new RunJobCommand(command.JobId, command.PointsNumber, command.RawArgumentsDictionary);
                var synchronousJobCommand = new RunJobSynchronouslyCommand(runJobCommand);
                _ = await mediator.Send(synchronousJobCommand, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during scheduled job processing.");
            }

            await jobCompletionNotifier.NotifyAsync(new JobCompletionNotification(job), command.CallbackUrl, stoppingToken);
            
            await mediator.Send(new DeleteJobCommand(job.Id), CancellationToken.None);
        }
    }
}