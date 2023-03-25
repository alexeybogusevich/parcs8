using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public sealed class MainModuleLoader : IMainModuleLoader
    {
        private readonly ITypeLoader<IMainModule> _typeLoader;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public MainModuleLoader(ITypeLoader<IMainModule> typeLoader, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _typeLoader = typeLoader;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        }

        public IMainModule Load(Guid moduleId, string assemblyName, string className = null)
        {
            var assemblyDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
            return _typeLoader.Load(assemblyDirectoryPath, assemblyName, className);
        }
    }
}