using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Parcs.Portal.Configuration;
using Serilog;

namespace Parcs.Portal.Extensions
{
    public static class ConfigureHostBuilderExtensions
    {
        public static void AddElasticSearchLogging(this ConfigureHostBuilder hostBuilder, IConfiguration configuration)
        {
            var elasticsearchConfiguration = configuration
                .GetSection(ElasticsearchConfiguration.SectionName)
                .Get<ElasticsearchConfiguration>();

            hostBuilder.UseSerilog((hostContext, logging) => logging
                .WriteTo.Elasticsearch([new Uri(elasticsearchConfiguration.BaseUrl)], options =>
                {
                    options.DataStream = new DataStreamName("parcs-portal");
                    options.BootstrapMethod = BootstrapMethod.Failure;
                })
                .ReadFrom.Configuration(hostContext.Configuration));
        }
    }
}