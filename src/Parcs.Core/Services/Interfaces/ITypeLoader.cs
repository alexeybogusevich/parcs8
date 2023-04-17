namespace Parcs.Core.Services.Interfaces
{
    public interface ITypeLoader<out T> where T : class
    {
        T Load(string assemblyDirectoryPath, string assemblyName, string className = null);
    }
}