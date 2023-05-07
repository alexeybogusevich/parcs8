using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Core.Models.Constants;
using Parcs.Core.Services.Interfaces;
using System.Net;

namespace Parcs.Core.Services
{
    public class KubernetesDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        //private const string KubernetesDomain = "svc.cluster.local";

        private readonly KubernetesConfiguration _configuration;

        public KubernetesDaemonResolutionStrategy(IOptions<KubernetesConfiguration> options)
        {
            _configuration = options.Value;
        }

        public IEnumerable<Daemon> Resolve() => Dns
                .GetHostAddresses($"{_configuration.DaemonsHeadlessServiceName}.{_configuration.NamespaceName}")
                .Select(
                    a => new Daemon { HostUrl = a.ToString(), Port = DaemonPorts.Default });
    }
}