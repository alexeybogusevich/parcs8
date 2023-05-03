namespace Parcs.Net
{
    public interface IModuleInfo : IAsyncDisposable
    {
        bool IsHost { get; }
        IChannel Parent { get; }
        IInputReader InputReader { get; }
        IOutputWriter OutputWriter { get; }
        IArgumentsProvider ArgumentsProvider { get; }
        Task<IPoint> CreatePointAsync();
    }
}