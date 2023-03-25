using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared;
using System.Reflection;

namespace Parcs.HostAPI.Services
{
    public class MainModuleLoader : IMainModuleLoader
    {
        private const string AssemblyExtension = "dll";

        private readonly IFileReader _fileReader;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public MainModuleLoader(IFileReader fileReader, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _fileReader = fileReader;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        }

        public IMainModule Load(Guid moduleId, string assemblyName, string className = null)
        {
            var mainModuleDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
            var mainModuleAssemblyPath = Path.Combine(mainModuleDirectoryPath, $"{assemblyName}.{AssemblyExtension}");

            var moduleLoadContext = new ModuleLoadContext(mainModuleAssemblyPath);
            var assembly = moduleLoadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(mainModuleAssemblyPath));
            var classes = assembly.GetTypes().Where(t => typeof(IMainModule).IsAssignableFrom(t));

            if (!classes.Any())
            {
                throw new ApplicationException(
                    $"Can't find any type which implements {nameof(IMainModule)} in {assembly.FullName}.\n" +
                    $"Available types: {string.Join(",", assembly.GetTypes().Select(t => t.FullName))}");
            }

            if (className is null)
            {
                return Activator.CreateInstance(classes.FirstOrDefault()) as IMainModule;
            }

            var @class = classes.FirstOrDefault(c => c.FullName == className || c.Name == className);

            if (@class is null)
            {
                throw new ApplicationException(
                    $"The requested class {className} does not implement {nameof(IMainModule)} in {assembly.FullName}.\n" +
                    $"Found implementations: {string.Join(",", classes.Select(t => t.FullName))}");
            }

            return Activator.CreateInstance(@class) as IMainModule;
        }
    }
}