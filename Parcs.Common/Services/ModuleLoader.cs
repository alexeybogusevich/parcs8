using System.Reflection;

namespace Parcs.Shared.Services
{
    public abstract class ModuleLoader
    {
        private const string AssemblyExtension = "dll";

        public abstract string GetModuleDirectoryPath();

        public TModule Load<TModule>(Guid moduleId, string assemblyName, string className = null)
            where TModule : class, IModule
        {
            var moduleDirectoryPath = GetModuleDirectoryPath();
            var moduleAssemblyPath = Path.Combine(moduleDirectoryPath, $"{assemblyName}.{AssemblyExtension}");

            var moduleLoadContext = new ModuleLoadContext(moduleAssemblyPath);
            var moduleAssembly = moduleLoadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(moduleAssemblyPath));
            var moduleClasses = moduleAssembly.GetTypes().Where(t => typeof(TModule).IsAssignableFrom(t));

            if (!moduleClasses.Any())
            {
                throw new ApplicationException(
                    $"Can't find any type which implements {nameof(TModule)} in {moduleAssembly.FullName}.\n" +
                    $"Available types: {string.Join(",", moduleAssembly.GetTypes().Select(t => t.FullName))}");
            }

            if (className is null)
            {
                return Activator.CreateInstance(moduleClasses.FirstOrDefault()) as TModule;
            }

            var @class = moduleClasses.FirstOrDefault(c => c.FullName == className || c.Name == className);

            if (@class is null)
            {
                throw new ApplicationException(
                    $"The requested class {className} does not implement {nameof(TModule)} in {moduleAssembly.FullName}.\n" +
                    $"Found implementations: {string.Join(",", moduleClasses.Select(t => t.FullName))}");
            }

            return Activator.CreateInstance(@class) as TModule;
        }
    }
}