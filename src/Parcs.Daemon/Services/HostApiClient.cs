using Flurl.Http;
using Microsoft.Extensions.Options;
using Parcs.Daemon.Configuration;
using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    public class HostApiClient(HttpClient httpClient, IOptions<HostConfiguration> options) : IHostApiClient
    {
        private readonly FlurlClient _flurlClient = new(httpClient);
        private readonly HostConfiguration _configuration = options.Value;

        public Task PostJobFailureAsync(PostJobFailureApiRequest request, CancellationToken cancellationToken = default)
        {
            var requestPath = string.Format(_configuration.JobFailuresPath);
            return _flurlClient.Request(requestPath).PostJsonAsync(request, cancellationToken: cancellationToken);
        }
    }
}