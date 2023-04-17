namespace Parcs.Core.Configuration
{
    public class KubernetesConfiguration
    {
        public const string SectionName = "Kubernetes";

        public string NamespaceName { get; set; }

        public string DaemonsHeadlessServiceName { get; set; }
    }
}