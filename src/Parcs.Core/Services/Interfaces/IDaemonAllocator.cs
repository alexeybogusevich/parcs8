using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    /// <summary>
    /// Places points on a static daemon pool. Shared by every job of the process, so concurrent
    /// jobs spread over the pool instead of each starting from the first daemon.
    /// </summary>
    public interface IDaemonAllocator
    {
        /// <summary>
        /// Picks the daemon of <paramref name="available"/> with the fewest active points; the
        /// point is released when the returned lease is disposed.
        /// </summary>
        DaemonLease Acquire(IReadOnlyList<Daemon> available);

        /// <summary>Active points per daemon (host:port).</summary>
        IReadOnlyDictionary<string, int> ActivePoints { get; }
    }

    public sealed class DaemonLease(Daemon daemon, Action release) : IDisposable
    {
        private Action _release = release;

        public Daemon Daemon { get; } = daemon;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
