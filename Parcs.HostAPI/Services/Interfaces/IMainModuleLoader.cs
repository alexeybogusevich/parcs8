using Parcs.Net;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IMainModuleLoader
    {
        Task<IMainModule> LoadAsync(Guid moduleId, string assemblyName, string className, CancellationToken cancellationToken = default);
    }
}