using Parcs.Net;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IModuleLoader
    {
        IModule Load(Guid moduleId, string assemblyName, string className = null);
    }
}