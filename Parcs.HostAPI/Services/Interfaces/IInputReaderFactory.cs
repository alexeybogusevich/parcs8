using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInputReaderFactory
    {
        IInputReader Create(Guid jobId);
    }
}