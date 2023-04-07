using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class HostInfoFactory : IHostInfoFactory
    {
        private readonly IDaemonResolver _daemonResolver;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IInputOutputFactory _inputOutputFactory;

        public HostInfoFactory(
            IDaemonResolver daemonResolver, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IInputOutputFactory inputOutputFactory)
        {
            _daemonResolver = daemonResolver;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _inputOutputFactory = inputOutputFactory;
        }

        public async Task<IHostInfo> CreateAsync(Job job)
        {
            var workerModulesPath = _moduleDirectoryPathBuilder.Build(job.ModuleId, ModuleDirectoryGroup.Worker);
            var inputReader = _inputOutputFactory.CreateReader(job);
            var outputWriter = _inputOutputFactory.CreateWriter(job);
            var daemons = await _daemonResolver.GetAvailableDaemonsAsync();
            return new HostInfo(job, daemons, workerModulesPath, inputReader, outputWriter);
        }
    }
}