using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parcs.Daemon.Extensions;
using Parcs.Core.Services;
using Parcs.Daemon.HostedServices;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<InternalServer>();
        services.AddHostedService<TcpServer>();
        services.AddHostedService(sp => sp.GetRequiredService<CallbackTcpServer>());
        services.AddHostedService<PointCreationConsumer>();
        services.AddApplicationServices();
        services.AddApplicationOptions(hostContext.Configuration);
        services.AddApplicationLogging(hostContext.Configuration);
        services.AddHttpClients(hostContext.Configuration);
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.ClearProviders();
        logging.AddElasticsearchLogging(hostingContext.Configuration);
    })
    .Build().RunAsync();