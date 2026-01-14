namespace Parcs.Core.Configuration
{
    public class ServiceBusConfiguration
    {
        public const string SectionName = "ServiceBus";

        public string ConnectionString { get; set; }

        public string QueueName { get; set; }
    }
}
