using Parcs.Net;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class ModuleInfoFactory : IModuleInfoFactory
    {
        private readonly IDaemonResolver _daemonResolver;
        private readonly IInputOutputFactory _inputOutputFactory;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory;

        public ModuleInfoFactory(
            IDaemonResolver daemonResolver,
            IInputOutputFactory inputOutputFactory,
            IArgumentsProviderFactory argumentsProviderFactory)
        {
            _daemonResolver = daemonResolver;
            _inputOutputFactory = inputOutputFactory;
            _argumentsProviderFactory = argumentsProviderFactory;
        }

        public IModuleInfo Create(
            Guid jobId,
            Guid moduleId,
            int pointsNumber,
            IDictionary<string, string> arguments,
            IChannel parentChannel = null,
            CancellationToken cancellationToken = default)
        {
            var argumentsProvider = _argumentsProviderFactory.Create(pointsNumber, arguments);

            return new ModuleInfo(
                jobId, moduleId, parentChannel, _inputOutputFactory, argumentsProvider, _daemonResolver, cancellationToken);
        }
    }
}