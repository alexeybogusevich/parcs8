using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;
using System.Threading.Channels;

namespace Parcs.HostAPI.Background
{
    public class ScheduledJobProcessor : BackgroundService
    {
        private readonly IMediator _mediator;
        private readonly IJobCompletionNotifier _jobCompletionNotifier;
        private readonly ChannelReader<ScheduleJobCommand> _channelReader;
        private readonly ILogger<ScheduledJobProcessor> _logger;

        public ScheduledJobProcessor(
            IMediator mediator,
            IJobCompletionNotifier jobCompletionNotifier,
            ChannelReader<ScheduleJobCommand> channelReader,
            ILogger<ScheduledJobProcessor> logger)
        {
            _mediator = mediator;
            _jobCompletionNotifier = jobCompletionNotifier;
            _channelReader = channelReader;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var scheduleJobCommand in _channelReader.ReadAllAsync(stoppingToken))
            {
                await HandleScheduledJobAsync(scheduleJobCommand, stoppingToken);
            }
        }

        private async Task HandleScheduledJobAsync(ScheduleJobCommand scheduleJobCommand, CancellationToken stoppingToken)
        {
            try
            {
                var runJobCommand = new RunJobCommand(scheduleJobCommand.ModuleId, scheduleJobCommand.InputFiles, scheduleJobCommand.Daemons);
                var response = await _mediator.Send(runJobCommand, stoppingToken);
                await _jobCompletionNotifier.NotifyAsync(response, scheduleJobCommand.CallbackUrl, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during scheduled job processing.");
            }
        }
    }
}