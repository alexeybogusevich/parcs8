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
        /// Opens an endpoint other points can connect to with <see cref="ConnectAsync"/>, which
        /// gives each pair of points a channel of their own. This is how points build topologies
        /// other than the star of parent channels as they run: rings, trees, hypercubes, grids.
        /// </summary>
        Task<IPeerEndpoint> OpenEndpointAsync();

        /// <summary>Opens a channel to the point that opened the endpoint at <paramref name="endpointAddress"/>.</summary>
        Task<IChannel> ConnectAsync(string endpointAddress);

        /// <summary>
        /// Joins two points this module created with a channel of their own, the way a
        /// configuring module builds a control space: create the points and their channels, link
        /// them, then execute their classes. Any module can do this for the points it creates,
        /// the main module or one running in a point, so each nested level of the control space
        /// can wire its own children. Call it before <see cref="IPoint.ExecuteClassAsync"/> on
        /// either point. The module running in each point gets its end with
        /// <see cref="GetLinkAsync"/>, under the name given for that point. Points created by
        /// different modules connect themselves instead, with <see cref="OpenEndpointAsync"/>
        /// and <see cref="ConnectAsync"/>.
        /// </summary>
        Task LinkPointsAsync(IPoint first, string nameAtFirst, IPoint second, string nameAtSecond);

        /// <summary>
        /// The channel the creating module linked to this point under <paramref name="name"/>
        /// with <see cref="LinkPointsAsync"/>; waits until the link is connected.
        /// </summary>
        Task<IChannel> GetLinkAsync(string name);
    }
}