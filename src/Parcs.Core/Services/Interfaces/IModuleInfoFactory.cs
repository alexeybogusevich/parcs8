using Parcs.Core.Models;
using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IModuleInfoFactory
    {
        IModuleInfo Create(
            JobMetadata jobMetadata,
            int pointsNumber,
            IDictionary<string, string> arguments,
            IChannel parentChannel = null,
            CancellationToken cancellationToken = default);

        IModuleInfo Create(
            JobMetadata jobMetadata,
            int pointsNumber,
            IDictionary<string, string> arguments,
            CancellationToken cancellationToken = default);
    }
}