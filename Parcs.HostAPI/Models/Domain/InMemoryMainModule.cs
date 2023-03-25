using Parcs.Net;

namespace Parcs.HostAPI.Models.Domain
{
    public sealed class InMemoryMainModule
    {
        private readonly IMainModule _mainModule;

        public InMemoryMainModule(IMainModule mainModule)
        {
            _mainModule = mainModule;
            CreateDateUtc = DateTime.UtcNow;
            LastTimeAccessedUtc = DateTime.UtcNow;
        }

        public DateTime CreateDateUtc { get; private set; }

        public DateTime LastTimeAccessedUtc { get; private set; }

        public IMainModule GetModule()
        {
            LastTimeAccessedUtc = DateTime.UtcNow;
            return _mainModule;
        }
    }
}