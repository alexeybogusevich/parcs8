using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    /// <inheritdoc/>
    public sealed class DaemonAllocator : IDaemonAllocator
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, int> _active = [];

        public IReadOnlyDictionary<string, int> ActivePoints
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, int>(_active);
                }
            }
        }

        public DaemonLease Acquire(IReadOnlyList<Daemon> available)
        {
            if (available is null || available.Count == 0)
            {
                throw new InvalidOperationException("No daemons available.");
            }

            lock (_lock)
            {
                // Least-loaded first; ties keep the order of the pool, so an idle pool fills in order.
                var daemon = available
                    .Select((d, index) => (Daemon: d, Index: index, Load: _active.GetValueOrDefault(Key(d))))
                    .OrderBy(c => c.Load)
                    .ThenBy(c => c.Index)
                    .First()
                    .Daemon;

                var key = Key(daemon);
                _active[key] = _active.GetValueOrDefault(key) + 1;
                return new DaemonLease(daemon, () => Release(key));
            }
        }

        private void Release(string key)
        {
            lock (_lock)
            {
                if (_active.TryGetValue(key, out var count))
                {
                    if (count <= 1) _active.Remove(key); else _active[key] = count - 1;
                }
            }
        }

        private static string Key(Daemon daemon) => $"{daemon.HostUrl}:{daemon.Port}";
    }
}
