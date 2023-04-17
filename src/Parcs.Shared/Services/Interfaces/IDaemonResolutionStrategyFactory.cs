using Parcs.Shared.Models.Enums;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IDaemonResolutionStrategyFactory
    {
        IDaemonResolutionStrategy Create(HostingEnvironment hostingEnvironment);
    }
}