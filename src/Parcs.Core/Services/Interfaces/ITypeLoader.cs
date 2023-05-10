namespace Parcs.Core.Services.Interfaces
{
    public interface ITypeLoader<out T> where T : class
    {
        T Load(string assemblyPath, string className = null);

        void Unload(string assemblyPath);
    }
}