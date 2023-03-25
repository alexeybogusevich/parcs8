namespace Parcs.Net
{
    public interface IMainModule : IModule
    {
        Task RunAsync(IHostInfo hostInfo, IInputReader inputReader, IOutputWriter outputWriter, CancellationToken cancellationToken = default);
    }
}