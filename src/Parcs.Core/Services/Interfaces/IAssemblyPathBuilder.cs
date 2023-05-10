namespace Parcs.Core.Services.Interfaces
{
    public interface IAssemblyPathBuilder
    {
        string Build(string assemblyDirectoryPath, string assemblyName);
    }
}