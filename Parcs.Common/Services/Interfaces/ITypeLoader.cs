namespace Parcs.Shared.Services.Interfaces
{
    public interface ITypeLoader<T> where T : class
    {
        T Load(string assemblyDirectory, string assemblyName, string className = null);
    }
}