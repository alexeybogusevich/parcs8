namespace Parcs.Daemon.Models
{
    public class JobContext(long jobId, long moduleId, int pointsNumber, IDictionary<string, string> arguments)
    {
        public long JobId { get; init; } = jobId;

        public long ModuleId { get; init; } = moduleId;

        public int PointsNumber { get; init; } = pointsNumber;

        public IDictionary<string, string> Arguments { get; init; } = arguments;

        public CancellationTokenSource CancellationTokenSource { get; init; } = new();
    }
}