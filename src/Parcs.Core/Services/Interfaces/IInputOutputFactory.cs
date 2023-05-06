using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IInputOutputFactory
    {
        IInputReader CreateReader(long jobId);
        IOutputWriter CreateWriter(long jobId, CancellationToken cancellationToken);
    }
}