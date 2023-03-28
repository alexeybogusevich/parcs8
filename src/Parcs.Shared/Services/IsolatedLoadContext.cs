using System.Reflection;
using System.Runtime.Loader;

namespace Parcs.Shared.Services
{
    public class IsolatedLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly IEnumerable<string> _sharedAssemblyNames;

        public IsolatedLoadContext(string assemblyPath, IEnumerable<string> sharedAssemblyNames)
        {
            _resolver = new AssemblyDependencyResolver(assemblyPath);
            _sharedAssemblyNames = sharedAssemblyNames;
            Resolving += OnFailedResolution;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }

        private Assembly OnFailedResolution(AssemblyLoadContext loadContext, AssemblyName requestedAssemblyName)
        {
            if (requestedAssemblyName?.Name is null || _sharedAssemblyNames.All(name => requestedAssemblyName.Name.StartsWith(name) is false))
            {
                return null;
            }

            var defaultContextAssembly = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == requestedAssemblyName.Name);

            if (defaultContextAssembly == null)
            {
                return null;
            }

            var defaultContextAssemblyVersion = defaultContextAssembly.GetName().Version;

            if (defaultContextAssemblyVersion == null || defaultContextAssemblyVersion.Major < requestedAssemblyName.Version.Major)
            {
                return null;
            }

            return defaultContextAssembly;
        }
    }
}