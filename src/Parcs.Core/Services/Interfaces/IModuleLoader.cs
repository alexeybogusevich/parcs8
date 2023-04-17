using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IModuleLoader
    {
        IModule Load(Guid moduleId, string assemblyName, string className = null);
    }
}