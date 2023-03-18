using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IMainModuleLoader
    {
        IMainModule Load(Guid moduleId, string className);
    }
}