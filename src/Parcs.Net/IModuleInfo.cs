using Microsoft.Extensions.Logging;

namespace Parcs.Net
{
    public interface IModuleInfo : IAsyncDisposable
    {
        bool IsHost { get; }

        IChannel Parent { get; }

        IInputReader InputReader { get; }

        IOutputWriter OutputWriter { get; }

        ILogger Logger { get; }

        T BindModuleOptions<T>() where T : class, IModuleOptions, new();

        Task<IPoint> CreatePointAsync();
    }
}