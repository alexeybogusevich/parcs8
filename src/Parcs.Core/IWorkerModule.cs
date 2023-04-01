namespace Parcs.Net
{
    public interface IWorkerModule : IModule
    {
        Task RunAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}