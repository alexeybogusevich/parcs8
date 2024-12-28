using Parcs.Net;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class ModuleLoader(
        ITypeLoader<IModule> typeLoader,
        IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
        IAssemblyPathBuilder assemblyPathBuilder) : IModuleLoader
    {
        private readonly ITypeLoader<IModule> _typeLoader = typeLoader;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        private readonly IAssemblyPathBuilder _assemblyPathBuilder = assemblyPathBuilder;

        public IModule Load(long moduleId, string assemblyName, string className = null)
        {
            var assemblyDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId);
            var assemblyPath = _assemblyPathBuilder.Build(assemblyDirectoryPath, assemblyName);
            return _typeLoader.Load(assemblyPath, className);
        }

        public void Unload(long moduleId, string assemblyName)
        {
            var assemblyDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId);
            var assemblyPath = _assemblyPathBuilder.Build(assemblyDirectoryPath, assemblyName);
            _typeLoader.Unload(assemblyPath);
        }
    }
}