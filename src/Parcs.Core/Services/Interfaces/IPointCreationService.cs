using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IPointCreationService
    {
        Task<IPoint> CreatePointAsync(long jobId, long moduleId, IDictionary<string, string> arguments, string daemonHostUrl, int daemonPort, CancellationToken cancellationToken = default);
    }
}