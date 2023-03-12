namespace Parcs.Core
{
    public interface IMainModule
    {
        Task<ModuleOutput> RunAsync(IHostInfo hostInfo, IInputReader inputReader, CancellationToken cancellationToken = default);
    }
}