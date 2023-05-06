using Parcs.Core.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.Core.Services
{
    public class IsolatedLoadContextProvider : IIsolatedLoadContextProvider
    {
        private readonly ConcurrentDictionary<string, IsolatedLoadContext> _cachedContexts = new ();

        public IsolatedLoadContext Create(string assemblyPath)
        {
            if (_cachedContexts.TryGetValue(assemblyPath, out var loadContext))
            {
                return loadContext;
            }

            loadContext = new IsolatedLoadContext(assemblyPath);

            _ = _cachedContexts.TryAdd(assemblyPath, loadContext);

            return loadContext;
        }

        public void Delete(string assemblyPath) => _cachedContexts.Remove(assemblyPath, out _);
    }
}