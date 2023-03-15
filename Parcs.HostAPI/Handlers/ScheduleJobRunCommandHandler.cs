using MediatR;
using Parcs.HostAPI.Models.Commands;
using System.Threading.Channels;

namespace Parcs.HostAPI.Handlers
{
    public class ScheduleJobRunCommandHandler : IRequestHandler<ScheduleJobRunCommand>
    {
        private readonly ChannelWriter<ScheduleJobRunCommand> _channelWriter;

        public ScheduleJobRunCommandHandler(ChannelWriter<ScheduleJobRunCommand> channelWriter)
        {
            _channelWriter = channelWriter;
        }

        public async Task Handle(ScheduleJobRunCommand request, CancellationToken cancellationToken)
        {
            await _channelWriter.WriteAsync(request, cancellationToken);
        }
    }
}