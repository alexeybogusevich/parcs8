using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    public sealed class CurrentPointRequestAccessor : ICurrentPointRequestAccessor
    {
        public PointCreationRequest Current { get; set; }
    }
}
