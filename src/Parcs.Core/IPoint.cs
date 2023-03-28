namespace Parcs.Net
{
    public interface IPoint
    {
        Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
        Task DeleteAsync();
    }
}