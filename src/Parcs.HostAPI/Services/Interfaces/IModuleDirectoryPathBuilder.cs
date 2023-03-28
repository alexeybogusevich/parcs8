using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IModuleDirectoryPathBuilder
    {
        string Build();
        string Build(Guid moduleId);
        string Build(Guid moduleId, ModuleDirectoryGroup directoryGroup);
    }
}