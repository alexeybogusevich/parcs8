using Parcs.Core.Models;
using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IModuleInfoFactory
    {
        IModuleInfo Create(
            JobMetadata jobMetadata,
            IDictionary<string, string> arguments,
            IChannel parentChannel = null,
            CancellationToken cancellationToken = default,
            PointLinks links = null);

        IModuleInfo Create(
            JobMetadata jobMetadata,
            IDictionary<string, string> arguments,
            CancellationToken cancellationToken = default);
    }
}