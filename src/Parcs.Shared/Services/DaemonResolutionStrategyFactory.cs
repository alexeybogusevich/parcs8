using Microsoft.Extensions.DependencyInjection;
using Parcs.Shared.Models.Enums;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public class DaemonResolutionStrategyFactory : IDaemonResolutionStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DaemonResolutionStrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IDaemonResolutionStrategy Create(HostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment switch
            {
                HostingEnvironment.Any => _serviceProvider.GetRequiredService<ConfigurationDaemonResolutionStrategy>(),
                HostingEnvironment.Kubernetes => _serviceProvider.GetRequiredService<KubernetesDaemonResolutionStrategy>(),
                _ => throw new NotImplementedException(),
            };
        }
    }
}