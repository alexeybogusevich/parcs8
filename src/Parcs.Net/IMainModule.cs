namespace Parcs.Net
{
    public interface IMainModule
    {
        Task RunAsync(IArgumentsProvider argumentsProvider, IHostInfo hostInfo, CancellationToken cancellationToken = default);
    }
}