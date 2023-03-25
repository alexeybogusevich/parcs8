namespace Parcs.Net
{
    public interface IWorkerModule
    {
        Task RunAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}