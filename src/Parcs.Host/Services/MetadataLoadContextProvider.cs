using Parcs.Host.Services.Interfaces;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Parcs.Host.Services
{
    public class MetadataLoadContextProvider : IMetadataLoadContextProvider
    {
        private const string RuntimeAssembly = "System.Runtime.dll";
        private const string CoreAssembly = "System.Private.CoreLib.dll";

        public MetadataLoadContext Get(params string[] sharedAssemblies)
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

            var pathToSystemRuntime = Path.Combine(runtimeDirectory, RuntimeAssembly);
            var pathToSystemPrivateCoreLib = Path.Combine(runtimeDirectory, CoreAssembly);

            var resolver = new PathAssemblyResolver(new List<string>(sharedAssemblies)
            {
                pathToSystemRuntime,
                pathToSystemPrivateCoreLib
            });

            return new MetadataLoadContext(resolver);
        }
    }
}