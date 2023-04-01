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
        private readonly IInputOutputFactory _inputOutputFactory;

        public HostInfoFactory(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IInputOutputFactory inputOutputFactory)
        {
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _inputOutputFactory = inputOutputFactory;
        }

        public IHostInfo Create(Job job, IEnumerable<Daemon> daemons)
        {
            var workerModulesPath = _moduleDirectoryPathBuilder.Build(job.ModuleId, ModuleDirectoryGroup.Worker);
            var inputReader = _inputOutputFactory.CreateReader(job);
            var outputWriter = _inputOutputFactory.CreateWriter(job);
            return new HostInfo(job, daemons, workerModulesPath, inputReader, outputWriter);
        }
    }
}