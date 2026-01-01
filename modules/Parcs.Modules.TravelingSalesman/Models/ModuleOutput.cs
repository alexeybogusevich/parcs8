namespace Parcs.Modules.TravelingSalesman.Models
{
    public class ModuleOutput
    {
        public double BestDistance { get; set; }
        public double AverageDistance { get; set; }
        public double ElapsedSeconds { get; set; }
        public int GenerationsCompleted { get; set; }
        public List<int> BestRoute { get; set; } = new();
        public List<double> ConvergenceHistory { get; set; } = new();
    }
} 