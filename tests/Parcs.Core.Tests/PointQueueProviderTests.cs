using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Core.Configuration;
using Parcs.Core.Messaging;

namespace Parcs.Core.Tests
{
    public class PointQueueProviderTests
    {
        private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
                .Build();

        [Test]
        public void ExplicitProviderWins()
        {
            var configuration = Configuration(
                ("PointQueue:Provider", "sqs"),
                ("PubSub:ProjectId", "parcs-gcp"));

            Assert.That(PointQueueServiceCollectionExtensions.ResolveProvider(configuration), Is.EqualTo(PointQueueProviders.Sqs));
        }

        [TestCase("PubSub:ProjectId", "parcs-gcp", PointQueueProviders.PubSub)]
        [TestCase("ServiceBus:ConnectionString", "Endpoint=sb://parcs/", PointQueueProviders.ServiceBus)]
        [TestCase("Sqs:QueueUrl", "https://sqs.eu-central-1.amazonaws.com/1/point-requested", PointQueueProviders.Sqs)]
        public void ProviderIsInferredFromItsSection(string key, string value, string expected)
        {
            Assert.That(PointQueueServiceCollectionExtensions.ResolveProvider(Configuration((key, value))), Is.EqualTo(expected));
        }

        [Test]
        public void NoBrokerMeansDirectTcp()
        {
            Assert.That(PointQueueServiceCollectionExtensions.ResolveProvider(Configuration()), Is.EqualTo(PointQueueProviders.None));
        }

        [Test]
        public void UnknownProviderIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PointQueueServiceCollectionExtensions.ResolveProvider(Configuration(("PointQueue:Provider", "Kafka"))));
        }

        [TestCase(PointQueueProviders.PubSub, typeof(PubSubPointRequestPublisher), typeof(PubSubPointRequestReceiver))]
        [TestCase(PointQueueProviders.ServiceBus, typeof(ServiceBusPointRequestPublisher), typeof(ServiceBusPointRequestReceiver))]
        [TestCase(PointQueueProviders.Sqs, typeof(SqsPointRequestPublisher), typeof(SqsPointRequestReceiver))]
        [TestCase(PointQueueProviders.None, typeof(DisabledPointQueue), typeof(DisabledPointQueue))]
        public async Task RegistersTransportForProvider(string provider, Type publisherType, Type receiverType)
        {
            await using var serviceProvider = new ServiceCollection()
                .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
                .AddPointQueue(Configuration(
                    ("PointQueue:Provider", provider),
                    ("ServiceBus:ConnectionString", "Endpoint=sb://parcs.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=a2V5")))
                .BuildServiceProvider();

            var publisher = serviceProvider.GetRequiredService<IPointRequestPublisher>();
            var receiver = serviceProvider.GetRequiredService<IPointRequestReceiver>();

            Assert.Multiple(() =>
            {
                Assert.That(publisher, Is.TypeOf(publisherType));
                Assert.That(receiver, Is.TypeOf(receiverType));
                Assert.That(publisher.IsEnabled, Is.EqualTo(provider != PointQueueProviders.None));
            });
        }
    }
}
