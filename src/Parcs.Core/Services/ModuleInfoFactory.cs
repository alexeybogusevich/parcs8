using Parcs.Net;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Parcs.Core.Services
{
    public sealed class ModuleInfoFactory(
        IDaemonResolver daemonResolver,
        IInputOutputFactory inputOutputFactory,
        IArgumentsProviderFactory argumentsProviderFactory,
        IInternalChannelManager internalChannelManager,
        IAddressResolver addressResolver,
        ILogger<ModuleInfoFactory> logger,
        IPointCreationService pointCreationService = null) : IModuleInfoFactory
    {
        private readonly IDaemonResolver _daemonResolver = daemonResolver;
        private readonly IInputOutputFactory _inputOutputFactory = inputOutputFactory;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory = argumentsProviderFactory;
        private readonly IInternalChannelManager _internalChannelManager = internalChannelManager;
        private readonly IAddressResolver _addressResolver = addressResolver;
        private readonly ILogger<ModuleInfoFactory> _logger = logger;
        private readonly IPointCreationService _pointCreationService = pointCreationService;

        public IModuleInfo Create(
            JobMetadata jobMetadata,
            IDictionary<string, string> arguments,
            IChannel parentChannel = null,
            CancellationToken cancellationToken = default)
        {
            var argumentsProvider = _argumentsProviderFactory.Create(arguments);

            return new ModuleInfo(
                jobMetadata,
                parentChannel,
                _inputOutputFactory,
                argumentsProvider,
                _daemonResolver,
                _internalChannelManager,
                _addressResolver,
                _logger,
                cancellationToken,
                _pointCreationService);
        }

        public IModuleInfo Create(
            JobMetadata jobMetadata,
            IDictionary<string, string> arguments,
            CancellationToken cancellationToken = default) => Create(jobMetadata, arguments, null, cancellationToken);
    }
}