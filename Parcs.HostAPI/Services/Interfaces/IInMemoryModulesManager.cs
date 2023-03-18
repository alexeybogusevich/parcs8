using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInMemoryModulesManager
    {
        bool TryGet(Guid moduleId, out IMainModule mainModule);
        void ConstructAndSave(Guid moduleId, byte[] rawAssembly, string className);
        bool TryRemove(Guid moduleId);
    }
}