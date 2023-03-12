namespace Parcs.Core
{
    public interface IWorkerModule
    {
        Task RunAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}