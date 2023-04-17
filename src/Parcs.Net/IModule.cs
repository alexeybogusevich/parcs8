namespace Parcs.Net
{
    public interface IModule
    {
        Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default);
    }
}