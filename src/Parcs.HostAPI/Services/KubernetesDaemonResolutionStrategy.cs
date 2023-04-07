using k8s;
using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Constants;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public class KubernetesDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        private readonly KubernetesConfiguration _configuration;

        public KubernetesDaemonResolutionStrategy(IOptions<KubernetesConfiguration> options)
        {
            _configuration = options.Value;
        }

        public async Task<IEnumerable<Daemon>> ResolveAsync()
        {
            var config = KubernetesClientConfiguration.InClusterConfig();
            var client = new Kubernetes(config);

            var service = await client.ReadNamespacedServiceAsync(_configuration.DaemonsHeadlessServiceName, _configuration.NamespaceName);
            var endpoints = await client.ListNamespacedEndpointsAsync(_configuration.NamespaceName, labelSelector: $"service={service.Metadata.Name}");

            return endpoints.Items
                .SelectMany(e => e.Subsets)
                .SelectMany(s => s.Addresses)
                .Select(a => new Daemon { HostUrl = a.Ip, Port = DaemonPorts.Default })
                .ToList();
        }
    }
}