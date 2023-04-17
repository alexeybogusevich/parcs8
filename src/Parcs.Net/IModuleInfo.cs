namespace Parcs.Net
{
    public interface IModuleInfo : IAsyncDisposable
    {
        IChannel Parent { get; }
        IInputReader InputReader { get; }
        IOutputWriter OutputWriter { get; }
        IArgumentsProvider ArgumentsProvider { get; }
        Task<IPoint> CreatePointAsync();
    }
}