using MediatR;
using Parcs.HostAPI.Models.Commands;
using System.Threading.Channels;

namespace Parcs.HostAPI.Handlers
{
    public class CreateAsynchronousJobRunCommandHandler : IRequestHandler<CreateAsynchronousJobRunCommand>
    {
        private readonly ChannelWriter<CreateAsynchronousJobRunCommand> _channelWriter;

        public CreateAsynchronousJobRunCommandHandler(ChannelWriter<CreateAsynchronousJobRunCommand> channelWriter)
        {
            _channelWriter = channelWriter;
        }

        public async Task Handle(CreateAsynchronousJobRunCommand request, CancellationToken cancellationToken)
        {
            await _channelWriter.WriteAsync(request, cancellationToken);
        }
    }
}