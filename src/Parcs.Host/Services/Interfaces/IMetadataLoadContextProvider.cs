using System.Reflection;

namespace Parcs.Host.Services.Interfaces
{
    public interface IMetadataLoadContextProvider
    {
        MetadataLoadContext Get(params string[] sharedAssemblies);
    }
}