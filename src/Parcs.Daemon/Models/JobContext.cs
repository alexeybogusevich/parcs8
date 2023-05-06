namespace Parcs.Daemon.Models
{
    public class JobContext
    {
        public JobContext(long jobId, long moduleId, int pointsNumber, IDictionary<string, string> arguments)
        {
            JobId = jobId;
            ModuleId = moduleId;
            PointsNumber = pointsNumber;
            Arguments = arguments;
            CancellationTokenSource = new();
        }

        public long JobId { get; init; }

        public long ModuleId { get; init; }

        public int PointsNumber { get; init; }

        public IDictionary<string, string> Arguments { get; init; }

        public CancellationTokenSource CancellationTokenSource { get; init; }
    }
}