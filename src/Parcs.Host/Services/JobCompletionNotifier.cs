using Flurl.Http;
using Parcs.Host.Models.Domain;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Services
{
    public sealed class JobCompletionNotifier(IHttpClientFactory httpClientFactory) : IJobCompletionNotifier
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task NotifyAsync(JobCompletionNotification notification, string subscriberUrl, CancellationToken cancellationToken = default)
        {
            using var flurlClient = new FlurlClient(_httpClientFactory.CreateClient());

            await flurlClient
                .Request(subscriberUrl)
                .PostJsonAsync(notification, cancellationToken: cancellationToken);
        }
    }
}