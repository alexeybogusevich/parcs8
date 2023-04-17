using Parcs.Net;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class ModuleLoader : IModuleLoader
    {
        private readonly ITypeLoader<IModule> _typeLoader;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public ModuleLoader(ITypeLoader<IModule> typeLoader, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _typeLoader = typeLoader;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        }

        public IModule Load(Guid moduleId, string assemblyName, string className = null)
        {
            var assemblyDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId);
            return _typeLoader.Load(assemblyDirectoryPath, assemblyName, className);
        }
    }
}