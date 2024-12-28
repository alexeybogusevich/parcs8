namespace Parcs.Host.Models.Responses.Nested
{
    public class AssemblyMetadataResponse(string assemblyName, IEnumerable<string> iModuleImplementations)
    {
        public string Name { get; set; } = assemblyName;

        public IEnumerable<string> IModuleImplementations { get; set; } = iModuleImplementations;
    }
}