using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IInputOutputFactory
    {
        IInputReader CreateReader(Guid jobId);
        IOutputWriter CreateWriter(Guid jobId, CancellationToken cancellationToken);
    }
}