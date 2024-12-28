using Microsoft.Extensions.DependencyInjection;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public class DaemonResolutionStrategyFactory(IServiceProvider serviceProvider) : IDaemonResolutionStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

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