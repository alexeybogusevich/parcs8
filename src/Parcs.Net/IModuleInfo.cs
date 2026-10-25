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

        /// <summary>
        /// Creates <paramref name="count"/> points by batch-publishing all Service Bus messages
        /// first, then awaiting all daemon connections concurrently. Prefer this over calling
        /// <see cref="CreatePointAsync"/> in a loop when using the KEDA scaling path.
        /// </summary>
        Task<IPoint[]> CreatePointsAsync(int count);

        /// <summary>
        /// Opens an endpoint another point can connect to with <see cref="ConnectAsync"/>, which
        /// gives the two points a channel of their own. This is how points form topologies other
        /// than the star of parent channels: rings, trees, hypercubes, grids.
        /// </summary>
        Task<IPeerEndpoint> OpenEndpointAsync();

        /// <summary>Opens a channel to the point that opened the endpoint at <paramref name="endpointAddress"/>.</summary>
        Task<IChannel> ConnectAsync(string endpointAddress);
    }
}