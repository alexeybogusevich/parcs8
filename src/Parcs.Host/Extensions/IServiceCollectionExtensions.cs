using FluentValidation;
using FluentValidation.AspNetCore;
using Parcs.Host.Configuration;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services;
using Parcs.Host.Services.Interfaces;
using Parcs.Host.Validators;
using Parcs.Core.Configuration;
using Parcs.Core.Services;
using Parcs.Core.Services.Interfaces;
using System.Reflection;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
using Parcs.Host.HostedServices;
using Parcs.Core.Models;
using Microsoft.EntityFrameworkCore;
using Parcs.Data.Context;
using System.Text;
using System.Text.Json.Serialization;

namespace Parcs.Host.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddAsynchronousJobProcessing(this IServiceCollection services)
        {
            return services
                .AddSingleton(Channel.CreateUnbounded<RunJobAsynchronouslyCommand>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<RunJobAsynchronouslyCommand>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<RunJobAsynchronouslyCommand>>().Writer)
                .AddHostedService<AsynchronousJobRunner>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<HostingConfiguration>(configuration.GetSection(HostingConfiguration.SectionName))
                .Configure<JobTrackingConfiguration>(configuration.GetSection(JobTrackingConfiguration.SectionName))
                .Configure<JobOutputConfiguration>(configuration.GetSection(JobOutputConfiguration.SectionName))
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<DaemonsConfiguration>(configuration.GetSection(DaemonsConfiguration.SectionName))
                .Configure<KubernetesConfiguration>(configuration.GetSection(KubernetesConfiguration.SectionName));
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IAddressResolver, AddressResolver>()
                .AddScoped<IAssemblyPathBuilder, AssemblyPathBuilder>()
                .AddScoped<IDaemonResolutionStrategyFactory, DaemonResolutionStrategyFactory>()
                .AddScoped<ConfigurationDaemonResolutionStrategy>()
                .AddScoped<KubernetesDaemonResolutionStrategy>()
                .AddScoped<IModuleInfoFactory, ModuleInfoFactory>()
                .AddScoped<IInputOutputFactory, InputOutputFactory>()
                .AddScoped<IJobCompletionNotifier, JobCompletionNotifier>()
                .AddScoped(typeof(ITypeLoader<>), typeof(TypeLoader<>))
                .AddScoped<IModuleLoader, ModuleLoader>()
                .AddScoped<IArgumentsProviderFactory, ArgumentsProviderFactory>()
                .AddScoped<IDaemonResolver, DaemonResolver>()
                .AddScoped<IJobDirectoryPathBuilder, JobDirectoryPathBuilder>()
                .AddScoped<IModuleDirectoryPathBuilder, ModuleDirectoryPathBuilder>()
                .AddScoped<IFileArchiver, FileArchiver>()
                .AddScoped<IFileSaver, FileSaver>()
                .AddScoped<IFileReader, FileReader>()
                .AddScoped<IFileEraser, FileEraser>()
                .AddSingleton<IJobTracker, JobTracker>()
                .AddSingleton<IInternalChannelManager, InternalChannelManager>()
                .AddSingleton<IIsolatedLoadContextProvider, IsolatedLoadContextProvider>()
                .AddSingleton(Channel.CreateUnbounded<InternalChannelReference>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Writer)
                .AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        }

        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var databaseConfiguration = configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .Get<DatabaseConfiguration>();

            var connectionString = new StringBuilder()
                .Append($"Host={databaseConfiguration.HostName};")
                .Append($"Port={databaseConfiguration.Port};")
                .Append($"Database={databaseConfiguration.DatabaseName};")
                .Append($"User ID={databaseConfiguration.Username};")
                .Append($"Password={databaseConfiguration.Password}")
                .ToString();

            return services.AddDbContext<ParcsDbContext>(options => options.UseNpgsql(connectionString));
        }

        public static IMvcBuilder AddApiControllers(this IServiceCollection services)
        {
            return services
                .AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }

        public static IServiceCollection AddValidation(this IServiceCollection services)
        {
            return services
                .AddValidatorsFromAssemblyContaining<GetJobQueryValidator>()
                .AddFluentValidationAutoValidation();
        }
    }
}