using Parcs.Core;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using System.Reflection;

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
            var rawAssembly = await _fileReader.ReadAsync(mainModuleDirectoryPath, assemblyName, cancellationToken);

            var assembly = Assembly.Load(rawAssembly);
            var @class = assembly.GetType(className) ?? throw new ArgumentException($"Class {className} not found in the assembly.");

            if (!@class.IsAssignableFrom(typeof(IMainModule)))
            {
                throw new ArgumentException($"Class {className} does not implement {nameof(IMainModule)}.");
            }

            return (IMainModule)Activator.CreateInstance(@class);
        }
    }
}