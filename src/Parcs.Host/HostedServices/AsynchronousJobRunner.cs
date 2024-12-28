using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Commands.Base;
using Parcs.Host.Models.Domain;
using Parcs.Host.Services.Interfaces;
using System.Threading.Channels;

namespace Parcs.Host.HostedServices
{
    public class AsynchronousJobRunner(
        IServiceScopeFactory serviceScopeFactory,
        ChannelReader<RunJobAsynchronouslyCommand> channelReader,
        ILogger<AsynchronousJobRunner> logger) : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ChannelReader<RunJobAsynchronouslyCommand> _channelReader = channelReader;
        private readonly ILogger<AsynchronousJobRunner> _logger = logger;

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

            try
            {
                var runJobCommand = new RunJobCommand(command.JobId, command.PointsNumber, command.Arguments);
                var synchronousJobCommand = new RunJobSynchronouslyCommand(runJobCommand);
                _ = await mediator.Send(synchronousJobCommand, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during scheduled job processing.");
            }

            await jobCompletionNotifier.NotifyAsync(new JobCompletionNotification(command.JobId), command.CallbackUrl, CancellationToken.None);
        }
    }
}