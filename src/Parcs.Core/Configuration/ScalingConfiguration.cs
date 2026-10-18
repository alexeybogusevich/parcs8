namespace Parcs.Core.Configuration
{
    /// <summary>
    /// Switches point allocation between the two deployment modes, so autoscaling can be turned
    /// off for controlled experiments and back on at any time without changing the code:
    ///
    ///   Elastic — each point is a message on the point queue; KEDA starts a daemon pod for it and
    ///             the node autoscaler adds nodes when needed (the configured PointQueue provider).
    ///   Static  — points go to a fixed pool of pre-started daemons (headless Service in
    ///             Kubernetes, Daemons:PreconfiguredInstances elsewhere); nothing is provisioned,
    ///             even if a point queue provider is configured.
    ///
    /// When <see cref="Mode"/> is empty, the mode follows the point queue: Elastic when a provider
    /// is configured, Static otherwise.
    /// </summary>
    public class ScalingConfiguration
    {
        public const string SectionName = "Scaling";

        /// <summary>One of <see cref="ScalingModes"/>; empty to infer.</summary>
        public string Mode { get; set; }
    }

    public static class ScalingModes
    {
        public const string Elastic = "Elastic";
        public const string Static = "Static";
    }
}
