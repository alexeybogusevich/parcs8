using Flurl.Http;
using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class JobCompletionNotifier : IJobCompletionNotifier
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public JobCompletionNotifier(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public Task NotifyAsync(JobCompletionNotification notification, string subscriberUrl, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;

            using var flurlClient = new FlurlClient(_httpClientFactory.CreateClient());
            return flurlClient.Request(subscriberUrl).PostJsonAsync(notification, cancellationToken);
        }
    }
}