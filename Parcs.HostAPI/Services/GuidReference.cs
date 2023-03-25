using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public sealed class GuidReference : IGuidReference
    {
        public Guid NewGuid() => Guid.NewGuid();
    }
}