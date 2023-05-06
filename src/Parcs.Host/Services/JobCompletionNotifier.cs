using Flurl.Http;
using Parcs.Host.Models.Domain;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Services
{
    public sealed class JobCompletionNotifier : IJobCompletionNotifier
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