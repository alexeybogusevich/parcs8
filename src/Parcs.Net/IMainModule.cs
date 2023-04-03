namespace Parcs.Net
{
    public interface IMainModule : IModule
    {
        Task RunAsync(IArgumentsProvider argumentsProvider, IHostInfo hostInfo, CancellationToken cancellationToken = default);
    }
}