using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;
using System.Threading.Channels;

namespace Parcs.HostAPI.Background
{
    public class ScheduledJobRunProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ChannelReader<ScheduleJobRunCommand> _channelReader;
        private readonly ILogger<ScheduledJobRunProcessor> _logger;

        public ScheduledJobRunProcessor(
            IServiceScopeFactory serviceScopeFactory,
            ChannelReader<ScheduleJobRunCommand> channelReader,
            ILogger<ScheduledJobRunProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _channelReader = channelReader;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var scheduleJobRunCommand in _channelReader.ReadAllAsync(stoppingToken))
            {
                await HandleCommandAsync(scheduleJobRunCommand, stoppingToken);
            }
        }

        private async Task HandleCommandAsync(ScheduleJobRunCommand command, CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var jobCompletionNotifier = scope.ServiceProvider.GetRequiredService<IJobCompletionNotifier>();

            try
            {
                var runJobCommand = new RunJobCommand { Daemons = command.Daemons, JobId = command.JobId };
                var response = await mediator.Send(runJobCommand, stoppingToken);
                await jobCompletionNotifier.NotifyAsync(response, command.CallbackUrl, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during scheduled job processing.");
            }
        }
    }
}