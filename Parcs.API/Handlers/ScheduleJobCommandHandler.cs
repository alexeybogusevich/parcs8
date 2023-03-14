using MediatR;
using Parcs.HostAPI.Models.Commands;
using System.Threading.Channels;

namespace Parcs.HostAPI.Handlers
{
    public class ScheduleJobCommandHandler : IRequestHandler<ScheduleJobCommand>
    {
        private readonly ChannelWriter<ScheduleJobCommand> _channelWriter;

        public ScheduleJobCommandHandler(ChannelWriter<ScheduleJobCommand> channelWriter)
        {
            _channelWriter = channelWriter;
        }

        public async Task Handle(ScheduleJobCommand request, CancellationToken cancellationToken)
        {
            await _channelWriter.WriteAsync(request, cancellationToken);
        }
    }
}