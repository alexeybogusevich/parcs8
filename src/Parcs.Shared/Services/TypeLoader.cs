using Parcs.Shared.Services.Interfaces;
using System.Reflection;

namespace Parcs.Shared.Services
{
    public class TypeLoader<T> : ITypeLoader<T> where T : class
    {
        private const string AssemblyExtension = "dll";

        public T Load(string assemblyDirectory, string assemblyName, string className = null)
        {
            var assemblyPath = Path.Combine(assemblyDirectory, $"{assemblyName}.{AssemblyExtension}");

            var loadContext = new IsolatedLoadContext(assemblyPath, new List<string> { typeof(T).Assembly.GetName().Name });
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