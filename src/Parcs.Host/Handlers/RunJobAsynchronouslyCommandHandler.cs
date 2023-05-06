using MediatR;
using Parcs.HostAPI.Models.Commands;
using System.Threading.Channels;

namespace Parcs.HostAPI.Handlers
{
    public sealed class RunJobAsynchronouslyCommandHandler : IRequestHandler<RunJobAsynchronouslyCommand>
    {
        private readonly ChannelWriter<RunJobAsynchronouslyCommand> _channelWriter;

        public RunJobAsynchronouslyCommandHandler(ChannelWriter<RunJobAsynchronouslyCommand> channelWriter)
        {
            _channelWriter = channelWriter;
        }

        public async Task Handle(RunJobAsynchronouslyCommand request, CancellationToken cancellationToken)
        {
            await _channelWriter.WriteAsync(request, cancellationToken);
        }
    }
}