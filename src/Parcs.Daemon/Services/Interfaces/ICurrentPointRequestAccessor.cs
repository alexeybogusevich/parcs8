using Parcs.Core.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    /// <summary>
    /// The point request this KEDA-provisioned pod is serving, set once by
    /// <see cref="HostedServices.PointCreationConsumer"/>. Null for statically deployed daemons.
    /// </summary>
    public interface ICurrentPointRequestAccessor
    {
        PointCreationRequest Current { get; set; }
    }
}
