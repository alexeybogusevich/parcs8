using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parcs.Core.Configuration;

namespace Parcs.Core.Messaging
{
    public static class PointQueueServiceCollectionExtensions
    {
        /// <summary>
        /// Binds the broker sections and registers the <see cref="IPointRequestPublisher"/> and
        /// <see cref="IPointRequestReceiver"/> for the configured provider.
        /// </summary>
        public static IServiceCollection AddPointQueue(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .Configure<PointQueueConfiguration>(configuration.GetSection(PointQueueConfiguration.SectionName))
                .Configure<PubSubConfiguration>(configuration.GetSection(PubSubConfiguration.SectionName))
                .Configure<ServiceBusConfiguration>(configuration.GetSection(ServiceBusConfiguration.SectionName))
                .Configure<SqsConfiguration>(configuration.GetSection(SqsConfiguration.SectionName))
                .Configure<ScalingConfiguration>(configuration.GetSection(ScalingConfiguration.SectionName));

            // Static mode gates the queue off: points go to the pre-started daemon pool.
            var provider = ResolveScalingMode(configuration) == ScalingModes.Static
                ? PointQueueProviders.None
                : ResolveProvider(configuration);

            switch (provider)
            {
                case PointQueueProviders.PubSub:
                    services.AddSingleton<IPointRequestPublisher, PubSubPointRequestPublisher>();
                    services.AddSingleton<IPointRequestReceiver, PubSubPointRequestReceiver>();
                    break;
                case PointQueueProviders.ServiceBus:
                    services.AddSingleton<IPointRequestPublisher, ServiceBusPointRequestPublisher>();
                    services.AddSingleton<IPointRequestReceiver, ServiceBusPointRequestReceiver>();
                    break;
                case PointQueueProviders.Sqs:
                    services.AddSingleton<IPointRequestPublisher, SqsPointRequestPublisher>();
                    services.AddSingleton<IPointRequestReceiver, SqsPointRequestReceiver>();
                    break;
                default:
                    services.AddSingleton<DisabledPointQueue>();
                    services.AddSingleton<IPointRequestPublisher>(sp => sp.GetRequiredService<DisabledPointQueue>());
                    services.AddSingleton<IPointRequestReceiver>(sp => sp.GetRequiredService<DisabledPointQueue>());
                    break;
            }

            return services;
        }

        /// <summary>
        /// Resolves <see cref="ScalingConfiguration.Mode"/>; when unset, Elastic if a point queue
        /// provider is configured and Static otherwise.
        /// </summary>
        public static string ResolveScalingMode(IConfiguration configuration)
        {
            var mode = configuration
                .GetSection(ScalingConfiguration.SectionName)
                .Get<ScalingConfiguration>()?.Mode;

            if (string.IsNullOrWhiteSpace(mode))
            {
                return ResolveProvider(configuration) == PointQueueProviders.None ? ScalingModes.Static : ScalingModes.Elastic;
            }

            return new[] { ScalingModes.Elastic, ScalingModes.Static }
                .FirstOrDefault(m => string.Equals(m, mode.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unknown scaling mode '{mode}'. Use Elastic or Static.");
        }

        public static string ResolveProvider(IConfiguration configuration)
        {
            var explicitProvider = configuration
                .GetSection(PointQueueConfiguration.SectionName)
                .Get<PointQueueConfiguration>()?.Provider;

            if (!string.IsNullOrWhiteSpace(explicitProvider))
            {
                return new[] { PointQueueProviders.None, PointQueueProviders.PubSub, PointQueueProviders.ServiceBus, PointQueueProviders.Sqs }
                    .FirstOrDefault(p => string.Equals(p, explicitProvider, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Unknown point queue provider '{explicitProvider}'.");
            }

            // Backward compatibility: earlier manifests only set the provider-specific section.
            var pubSub = configuration.GetSection(PubSubConfiguration.SectionName).Get<PubSubConfiguration>();
            if (!string.IsNullOrEmpty(pubSub?.ProjectId))
            {
                return PointQueueProviders.PubSub;
            }

            var serviceBus = configuration.GetSection(ServiceBusConfiguration.SectionName).Get<ServiceBusConfiguration>();
            if (!string.IsNullOrEmpty(serviceBus?.ConnectionString))
            {
                return PointQueueProviders.ServiceBus;
            }

            var sqs = configuration.GetSection(SqsConfiguration.SectionName).Get<SqsConfiguration>();
            if (!string.IsNullOrEmpty(sqs?.QueueUrl) || !string.IsNullOrEmpty(sqs?.GpuQueueUrl))
            {
                return PointQueueProviders.Sqs;
            }

            return PointQueueProviders.None;
        }
    }
}
