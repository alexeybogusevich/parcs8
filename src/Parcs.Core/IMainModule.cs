namespace Parcs.Net
{
    public interface IMainModule : IModule
    {
        Task RunAsync(IReadOnlyDictionary<string, string> arguments, IHostInfo hostInfo, CancellationToken cancellationToken = default);
    }
}