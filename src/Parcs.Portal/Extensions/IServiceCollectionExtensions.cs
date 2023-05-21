using Parcs.Portal.Services.Interfaces;
using Parcs.Portal.Services;
using Parcs.Core.Configuration;
using Parcs.Portal.Configuration;

namespace Parcs.Portal.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services.AddScoped<IHostClient, HostClient>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<HostingConfiguration>(configuration.GetSection(HostingConfiguration.SectionName))
                .Configure<HostConfiguration>(configuration.GetSection(HostConfiguration.SectionName))
                .Configure<PortalConfiguration>(configuration.GetSection(PortalConfiguration.SectionName));
        }

        public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            var hostConfiguration = configuration
                .GetSection(HostConfiguration.SectionName)
                .Get<HostConfiguration>();

            services.AddHttpClient<IHostClient, HostClient>(client =>
            {
                client.BaseAddress = new Uri($"http://{hostConfiguration.Uri}:80");
            });

            return services;
        }
    }
}