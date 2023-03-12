namespace Parcs.Core
{
    public sealed class Job
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Job()
        {
            Id = Guid.NewGuid();
            CreateDateUtc = DateTime.UtcNow;
            Status = JobStatus.New;
            _cancellationTokenSource = new ();
        }

        public Guid Id { get; private set; }

        public JobStatus Status { get; private set; }

        public DateTime CreateDateUtc { get; private set; }

        public DateTime? StartDateUtc { get; private set; }

        public DateTime? EndDateUtc { get; private set; }

        public double? Result { get; private set; }

        public string ErrorMessage { get; private set; }

        public IEnumerable<Daemon> CanBeExecutedOnDaemons { get; private set; }

        public TimeSpan? ExecutionTime => EndDateUtc is null ? default : EndDateUtc - StartDateUtc;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Start()
        {
            StartDateUtc = DateTime.UtcNow;
            Status = JobStatus.InProgress;
        }

        public void Finish(double result)
        {
            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Finished;
            Result = result;
        }

        public void Fail(string errorMessage)
        {
            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Error;
            ErrorMessage = errorMessage;
        }

        public void Abort()
        {
            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Aborted;
            _cancellationTokenSource.Cancel();
        }

        public void SetDaemons(IEnumerable<Daemon> daemons)
        {
            CanBeExecutedOnDaemons = daemons;
        }
    }
}