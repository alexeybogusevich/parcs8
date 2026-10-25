using Parcs.Net;
using System.Collections.Concurrent;

namespace Parcs.Core.Models
{
    /// <summary>
    /// The links the creating module set up for one point with
    /// <see cref="IModuleInfo.LinkPointsAsync"/>, by the name given for this point. Each is the
    /// connection in progress: the daemon dials or accepts it before the point's module starts.
    /// </summary>
    public sealed class PointLinks
    {
        private readonly ConcurrentDictionary<string, Task<IChannel>> _links = new(StringComparer.Ordinal);

        public bool TryAdd(string name, Task<IChannel> connection) => _links.TryAdd(name, connection);

        public Task<IChannel> GetAsync(string name) => _links.TryGetValue(name, out var connection)
            ? connection
            : throw new KeyNotFoundException(_links.IsEmpty
                ? $"This point has no link named '{name}'; its creator linked it to no other point."
                : $"This point has no link named '{name}'; its links are {string.Join(", ", _links.Keys.Order().Select(k => $"'{k}'"))}.");
    }
}
