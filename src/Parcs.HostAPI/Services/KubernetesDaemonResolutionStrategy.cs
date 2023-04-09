using k8s;
using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Constants;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;
using System.Net;

namespace Parcs.HostAPI.Services
{
    public class KubernetesDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        private readonly KubernetesConfiguration _configuration;
        private readonly ILogger<KubernetesDaemonResolutionStrategy> _logger;

        public KubernetesDaemonResolutionStrategy(IOptions<KubernetesConfiguration> options, ILogger<KubernetesDaemonResolutionStrategy> logger)
        {
            _configuration = options.Value;
            _logger = logger;
        }

        public async Task<IEnumerable<Daemon>> ResolveAsync()
        {
            var ipAddresses = Dns.GetHostAddresses($"{_configuration.DaemonsHeadlessServiceName}.{_configuration.NamespaceName}.svc.cluster.local");
            _logger.LogInformation(string.Join(" ", ipAddresses.Select(a => a.ToString())));

            //var config = KubernetesClientConfiguration.InClusterConfig();
            //var client = new Kubernetes(config);

            //var service = await client.ReadNamespacedServiceAsync(_configuration.DaemonsHeadlessServiceName, _configuration.NamespaceName);
            //var endpoints = await client.ListNamespacedEndpointsAsync(_configuration.NamespaceName, labelSelector: $"service={service.Metadata.Name}");

            //return endpoints.Items
            //    .SelectMany(e => e.Subsets)
            //    .SelectMany(s => s.Addresses)
            //    .Select(a => new Daemon { HostUrl = a.Ip, Port = DaemonPorts.Default })
            //    .ToList();

            return ipAddresses.Select(a => new Daemon { HostUrl = a.ToString(), Port = DaemonPorts.Default });
        }
    }
}