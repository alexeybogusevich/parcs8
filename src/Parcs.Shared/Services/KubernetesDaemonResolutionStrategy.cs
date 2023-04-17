using Microsoft.Extensions.Options;
using Parcs.Shared.Configuration;
using Parcs.Shared.Models;
using Parcs.Shared.Models.Constants;
using Parcs.Shared.Services.Interfaces;
using System.Net;

namespace Parcs.Shared.Services
{
    public class KubernetesDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        private const string KubernetesDomain = "svc.cluster.local";

        private readonly KubernetesConfiguration _configuration;

        public KubernetesDaemonResolutionStrategy(IOptions<KubernetesConfiguration> options)
        {
            _configuration = options.Value;
        }

        public IEnumerable<Daemon> Resolve() => Dns
                .GetHostAddresses($"{_configuration.DaemonsHeadlessServiceName}.{_configuration.NamespaceName}.{KubernetesDomain}")
                .Select(
                    a => new Daemon { HostUrl = a.ToString(), Port = DaemonPorts.Default });
    }
}