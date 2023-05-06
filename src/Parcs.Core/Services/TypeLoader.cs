using Parcs.Core.Services.Interfaces;
using System.Reflection;

namespace Parcs.Core.Services
{
    public class TypeLoader<T> : ITypeLoader<T> where T : class
    {
        private const string AssemblyExtension = "dll";

        private readonly IIsolatedLoadContextProvider _isolatedLoadContextProvider;

        public TypeLoader(IIsolatedLoadContextProvider isolatedLoadContextProvider)
        {
            _isolatedLoadContextProvider = isolatedLoadContextProvider;
        }

        public T Load(string assemblyDirectoryPath, string assemblyName, string className = null)
        {
            var assemblyPath = Path.Combine(assemblyDirectoryPath, $"{assemblyName}.{AssemblyExtension}");

            var loadContext = _isolatedLoadContextProvider.Create(assemblyPath);
            loadContext.AddSharedAssembly(typeof(T).Assembly.GetName().Name);
            
            var assembly = loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(assemblyPath));
            var classes = assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t));

            if (!classes.Any())
            {
                throw new ArgumentException(
                    $"Can't find any type which implements {typeof(T).Name} in {assembly.FullName}.\n" +
                    $"Available types: {string.Join(",", assembly.GetTypes().Select(t => t.FullName))}");
            }

            if (className is null)
            {
                return Activator.CreateInstance(classes.FirstOrDefault()) as T;
            }

            var @class = classes.FirstOrDefault(c => c.FullName == className || c.Name == className);

            if (@class is null)
            {
                throw new ArgumentException(
                    $"The requested class {className} does not implement {typeof(T).Name} in {assembly.FullName}.\n" +
                    $"Found implementations: {string.Join(",", classes.Select(t => t.FullName))}");
            }

            return Activator.CreateInstance(@class) as T;
        }
    }
}