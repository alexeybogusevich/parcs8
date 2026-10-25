using System.Runtime.CompilerServices;
using Parcs.Core.Models;
using Parcs.Core.Models.Interfaces;

namespace Parcs.Daemon.Services
{
    /// <summary>
    /// The links of each point this daemon hosts, keyed by the point's control channel: the
    /// OpenLink and ConnectLink signals add to them before ExecuteClass hands them to the module.
    /// An entry goes away with its channel.
    /// </summary>
    public sealed class PointLinkRegistry
    {
        private readonly ConditionalWeakTable<IManagedChannel, PointLinks> _links = new();

        public PointLinks For(IManagedChannel pointChannel) => _links.GetValue(pointChannel, _ => new PointLinks());
    }
}
