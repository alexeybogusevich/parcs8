namespace Parcs.Net
{
    public interface IPoint : IAsyncDisposable
    {
        Guid Id { get; }
        Task<IChannel> CreateChannelAsync();
        Task ExecuteClassAsync(string assemblyName, string className);
        Task DeleteAsync();
    }
}