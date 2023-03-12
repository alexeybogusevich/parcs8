namespace Parcs.Core
{
    public interface IMainModule
    {
        Task<ModuleOutput> RunAsync(IHostInfo hostInfo, CancellationToken cancellationToken = default);
    }
}