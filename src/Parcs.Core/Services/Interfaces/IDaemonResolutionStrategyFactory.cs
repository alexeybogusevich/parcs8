using Parcs.Core.Models.Enums;

namespace Parcs.Core.Services.Interfaces
{
    public interface IDaemonResolutionStrategyFactory
    {
        IDaemonResolutionStrategy Create(HostingEnvironment hostingEnvironment);
    }
}