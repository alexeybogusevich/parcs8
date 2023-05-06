using Flurl.Http;
using Microsoft.Extensions.Options;
using Parcs.Daemon.Configuration;
using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    public class HostApiClient : IHostApiClient
    {
        private readonly FlurlClient _flurlClient;
        private readonly HostApiConfiguration _configuration;

        public HostApiClient(HttpClient httpClient, IOptions<HostApiConfiguration> options)
        {
            _flurlClient = new FlurlClient(httpClient);
            _configuration = options.Value;
        }

        public Task PostJobFailureAsync(PostJobFailureApiRequest request, CancellationToken cancellationToken = default)
        {
            var requestPath = string.Format(_configuration.JobFailuresPath);
            return _flurlClient.Request(requestPath).PostJsonAsync(request, cancellationToken);
        }
    }
}