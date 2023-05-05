using Flurl.Http;
using Microsoft.Extensions.Options;
using Parcs.Daemon.Configuration;
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

        public Task PutCancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            var requestPath = string.Format(_configuration.JobCancellationPath, jobId);
            return _flurlClient.Request(requestPath).PutAsync();
        }
    }
}