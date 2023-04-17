using Parcs.Net;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IModuleInfoFactory
    {
        IModuleInfo Create(
            Guid jobId,
            Guid moduleId,
            int pointsNumber,
            IDictionary<string, string> arguments,
            IChannel parentChannel = null,
            CancellationToken cancellationToken = default);
    }
}