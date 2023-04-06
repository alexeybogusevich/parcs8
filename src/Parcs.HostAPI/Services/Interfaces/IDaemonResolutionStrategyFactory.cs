using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonResolutionStrategyFactory
    {
        IDaemonResolutionStrategy Create(HostingEnvironment hostingEnvironment);
    }
}