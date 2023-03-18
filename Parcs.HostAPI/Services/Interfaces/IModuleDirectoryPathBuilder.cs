using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IModuleDirectoryPathBuilder
    {
        string Build(Guid moduleId, ModuleDirectoryGroup directoryGroup);
    }
}