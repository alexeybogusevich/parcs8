namespace Parcs.Portal.Models.Host.Responses.Nested
{
    public class AssemblyMetadataResponse
    {
        public string Name { get; set; }

        public IEnumerable<string> IModuleImplementations { get; set; }
    }
}