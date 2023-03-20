using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInputOutputFactory
    {
        IInputReader CreateReader(Guid jobId);
        IOutputWriter CreateWriter(Guid jobId);
    }
}