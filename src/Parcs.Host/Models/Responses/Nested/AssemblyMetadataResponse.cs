namespace Parcs.Host.Models.Responses.Nested
{
    public class AssemblyMetadataResponse
    {
        public AssemblyMetadataResponse(string assemblyName, IEnumerable<string> iModuleImplementations)
        {
            Name = assemblyName;
            IModuleImplementations = iModuleImplementations;
        }

        public string Name { get; set; }

        public IEnumerable<string> IModuleImplementations { get; set; }
    }
}