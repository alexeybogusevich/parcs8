namespace Parcs.Net
{
    public interface IPoint : IAsyncDisposable
    {
        Task<IChannel> CreateChannelAsync();
        Task ExecuteClassAsync(string assemblyName, string className);
        Task DeleteAsync();
    }
}