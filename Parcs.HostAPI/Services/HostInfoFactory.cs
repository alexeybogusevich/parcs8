using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared;

namespace Parcs.HostAPI.Services
{
    public class HostInfoFactory : IHostInfoFactory
    {
        public IHostInfo Create(Job job, IEnumerable<Daemon> daemons) => new HostInfo(job, daemons);
    }
}