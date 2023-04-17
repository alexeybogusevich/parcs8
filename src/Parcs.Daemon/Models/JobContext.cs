namespace Parcs.Daemon.Models
{
    public class JobContext
    {
        private readonly List<Exception> _exceptions;

        public JobContext(Guid jobId, Guid moduleId, int pointsNumber, IDictionary<string, string> arguments)
        {
            JobId = jobId;
            ModuleId = moduleId;
            PointsNumber = pointsNumber;
            Arguments = arguments;
            CancellationTokenSource = new();
            _exceptions = new();
        }

        public Guid JobId { get; init; }

        public Guid ModuleId { get; init; }

        public int PointsNumber { get; init; }

        public IDictionary<string, string> Arguments { get; init; }

        public CancellationTokenSource CancellationTokenSource { get; init; }

        public IEnumerable<Exception> Exceptions => _exceptions;

        public void TrackException(Exception exception)
        {
            _exceptions.Add(exception);
        }
    }
}