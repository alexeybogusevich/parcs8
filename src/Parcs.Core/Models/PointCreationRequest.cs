namespace Parcs.Core.Models
{
    public class PointCreationRequest
    {
        public long JobId { get; set; }

        public long ModuleId { get; set; }

        public IDictionary<string, string> Arguments { get; set; }

        public string HostUrl { get; set; }

        public int HostPort { get; set; }

        public string CorrelationId { get; set; }
    }
}
