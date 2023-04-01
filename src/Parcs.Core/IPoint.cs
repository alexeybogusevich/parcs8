namespace Parcs.Net
{
    public interface IPoint
    {
        Task<IChannel> CreateChannelAsync();
        Task ExecuteClassAsync(string assemblyName, string className);
        Task DeleteAsync();
    }
}