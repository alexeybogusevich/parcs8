using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class HostInfoFactory : IHostInfoFactory
    {
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public HostInfoFactory(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        }

        public IHostInfo Create(Job job, IEnumerable<Daemon> daemons)
        {
            var workerModulesPath = _moduleDirectoryPathBuilder.Build(job.ModuleId, ModuleDirectoryGroup.Worker);
            return new HostInfo(job, daemons, workerModulesPath);
        }
    }
}