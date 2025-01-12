using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parcs.Daemon.Configuration;
using Serilog;

namespace Parcs.Daemon.Extensions
{
    public static class ILoggingBuilderExtensions
    {
        public static void AddElasticsearchLogging(this ILoggingBuilder loggingBuilder, IConfiguration configuration)
        {
            var elasticsearchConfiguration = configuration
                .GetSection(ElasticsearchConfiguration.SectionName)
                .Get<ElasticsearchConfiguration>();

            loggingBuilder.AddSerilog(new LoggerConfiguration()
                .WriteTo.Elasticsearch([new Uri(elasticsearchConfiguration.BaseUrl)], opts =>
                {
                    opts.DataStream = new DataStreamName("parcs-daemon");
                    opts.BootstrapMethod = BootstrapMethod.Failure;
                })
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger());
        }
    }
}