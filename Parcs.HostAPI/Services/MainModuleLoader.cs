using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Input;

namespace Parcs.HostAPI.Services
{
    public class MainModuleLoader : IMainModuleLoader
    {
        private readonly IFileReader _fileReader;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public MainModuleLoader(IFileReader fileReader, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _fileReader = fileReader;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        }

        public async Task<IMainModule> LoadAsync(Guid moduleId, string assemblyName, string className, CancellationToken cancellationToken = default)
        {
            var mainModuleDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
            //var mainModuleFile = await _fileReader.ReadAsync(mainModuleDirectoryPath, assemblyName, cancellationToken);

            var assemblyFullName = Path.Combine(mainModuleDirectoryPath, assemblyName);
            var moduleLoadContext = new ModuleLoadContext(assemblyFullName);
            var assemblyName1 = AssemblyName.GetAssemblyName(assemblyFullName);

            var assembly = moduleLoadContext.LoadFromAssemblyName(assemblyName1);
            var @class = assembly.GetTypes().FirstOrDefault(t => typeof(IMainModule).IsAssignableFrom(t));

            if (@class == null)
            {
                throw new ApplicationException(
                    $"Can't find any type which implements {nameof(IMainModule)} in {assembly}.\n" +
                    $"Available types: {string.Join(",", assembly.GetTypes().Select(t => t.FullName))}");
            }

            return Activator.CreateInstance(@class) as IMainModule;
        }
    }
}