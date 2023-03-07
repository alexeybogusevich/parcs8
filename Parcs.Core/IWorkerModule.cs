namespace Parcs.Core
{
    public interface IWorkerModule
    {
        void Run(IChannel channel, string input = null, CancellationToken cancellationToken = default);
    }
}