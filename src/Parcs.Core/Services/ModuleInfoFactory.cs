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
        private readonly IInternalChannelManager _internalChannelManager;
        private readonly IAddressResolver _addressResolver;

        public ModuleInfoFactory(
            IDaemonResolver daemonResolver,
            IInputOutputFactory inputOutputFactory,
            IArgumentsProviderFactory argumentsProviderFactory,
            IInternalChannelManager internalChannelManager,
            IAddressResolver addressResolver)
        {
            _daemonResolver = daemonResolver;
            _inputOutputFactory = inputOutputFactory;
            _argumentsProviderFactory = argumentsProviderFactory;
            _internalChannelManager = internalChannelManager;
            _addressResolver = addressResolver;
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
                jobId,
                moduleId,
                parentChannel,
                _inputOutputFactory,
                argumentsProvider,
                _daemonResolver,
                _internalChannelManager,
                _addressResolver,
                cancellationToken);
        }

        public IModuleInfo Create(
            Guid jobId,
            Guid moduleId,
            int pointsNumber,
            IDictionary<string, string> arguments,
            CancellationToken cancellationToken = default) => Create(jobId, moduleId, pointsNumber, arguments, null, cancellationToken);
    }
}