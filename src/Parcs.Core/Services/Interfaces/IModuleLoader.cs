using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IModuleLoader
    {
        IModule Load(long moduleId, string assemblyName, string className = null);

        void Unload(long moduleId, string assemblyName);
    }
}