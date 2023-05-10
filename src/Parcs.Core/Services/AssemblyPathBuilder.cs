using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public class AssemblyPathBuilder : IAssemblyPathBuilder
    {
        private const string AssemblyExtension = "dll";

        public string Build(string assemblyDirectoryPath, string assemblyName)
        {
            return Path.Combine(assemblyDirectoryPath, $"{assemblyName}.{AssemblyExtension}");
        }
    }
}