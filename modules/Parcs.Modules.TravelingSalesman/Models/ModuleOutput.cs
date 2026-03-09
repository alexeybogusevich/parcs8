namespace Parcs.Modules.TravelingSalesman.Models
{
    public class ModuleOutput
    {
        public double BestDistance { get; set; }
        public double AverageDistance { get; set; }

        /// <summary>Total wall-clock time from module start to result written.</summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// Time spent waiting for KEDA to schedule daemon pods and for all TCP channels
        /// to be established (infrastructure overhead, not computation).
        /// Only set by parallel main modules; 0 for sequential.
        /// </summary>
        public double PodProvisioningSeconds { get; set; }

        /// <summary>
        /// Time from all channels being ready to the final result being collected
        /// (actual genetic algorithm computation time).
        /// Only set by parallel main modules; equals ElapsedSeconds for sequential.
        /// </summary>
        public double ComputeSeconds { get; set; }

        public int GenerationsCompleted { get; set; }
        public List<int> BestRoute { get; set; } = new();
        public List<double> ConvergenceHistory { get; set; } = new();
    }
}