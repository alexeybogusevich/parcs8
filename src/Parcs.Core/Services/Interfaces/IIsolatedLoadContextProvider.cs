namespace Parcs.Core.Services.Interfaces
{
    public interface IIsolatedLoadContextProvider
    {
        IsolatedLoadContext Create(string assemblyPath);
        void Delete(string assemblyPath);
    }
}