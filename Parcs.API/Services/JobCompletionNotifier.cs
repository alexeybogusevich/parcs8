using Flurl.Http;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class JobCompletionNotifier : IJobCompletionNotifier
    {
        private readonly FlurlClient _flurlClient;

        public JobCompletionNotifier(IHttpClientFactory httpClientFactory)
        {
            _flurlClient = new(httpClientFactory.CreateClient());
        }

        public Task NotifyAsync(RunJobCommandResponse response, string callbackUrl, CancellationToken cancellationToken = default)
        {
            return _flurlClient.Request(callbackUrl).PostJsonAsync(response, cancellationToken);
        }
    }
}