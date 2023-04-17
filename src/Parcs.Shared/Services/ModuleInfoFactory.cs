using Parcs.Net;
using Parcs.Shared.Models;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public sealed class ModuleInfoFactory : IModuleInfoFactory
    {
        private readonly IDaemonsResolver _daemonResolver;
        private readonly IInputOutputFactory _inputOutputFactory;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory;

        public ModuleInfoFactory(
            IDaemonsResolver daemonResolver,
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