using Parcs.Net;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IMainModuleLoader
    {
        IMainModule Load(Guid moduleId, string assemblyName, string className = null);
    }
}