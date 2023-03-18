using Microsoft.Extensions.Options;
using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace Parcs.HostAPI.Services
{
    public class InMemoryModulesManager : IInMemoryModulesManager
    {
        private readonly ConcurrentDictionary<Guid, InMemoryMainModule> _inMemoryModules = new();
        private readonly InMemoryModulesConfiguration _configuration;

        public InMemoryModulesManager(IOptions<InMemoryModulesConfiguration> options)
        {
            _configuration = options.Value;
        }

        public void ConstructAndSave(Guid moduleId, byte[] rawAssembly, string className)
        {
            if (_inMemoryModules.Count == _configuration.MaximumNumberOfSimultaneouslyStoredModules)
            {
                var leastInteractedModule = _inMemoryModules.OrderBy(m => m.Value.LastTimeAccessedUtc).FirstOrDefault();
                _ = _inMemoryModules.TryRemove(leastInteractedModule.Key, out _);
            }

            var assembly = Assembly.Load(rawAssembly);
            var @class = assembly.GetType(className) ?? throw new ArgumentException($"Class {className} not found in the assembly.");
            
            if (!@class.IsAssignableFrom(typeof(IMainModule)))
            {
                throw new ArgumentException($"Class {className} does not implement {nameof(IMainModule)}.");
            }

            var inMemoryMainModule = new InMemoryMainModule((IMainModule)Activator.CreateInstance(@class));

            if (!_inMemoryModules.TryAdd(moduleId, inMemoryMainModule))
            {
                throw new SystemException($"Can't add module {moduleId} to the collection of in-memory modules.");
            }
        }

        public bool TryGet(Guid moduleId, out IMainModule mainModule)
        {
            var wasFound = _inMemoryModules.TryGetValue(moduleId, out var inMemoryModule);
            mainModule = inMemoryModule?.GetModule();
            return wasFound;
        }

        public bool TryRemove(Guid moduleId) => _inMemoryModules.TryRemove(moduleId, out _);
    }
}