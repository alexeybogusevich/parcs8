using Parcs.HostAPI.Services;

namespace Parcs.Core
{
    public sealed class Job : IObservable<JobCompletionNotification>
    {
        private bool _hasBeenRun;
        private bool _canBeCancelled;
        private readonly List<IObserver<JobCompletionNotification>> _observers;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Job(Guid moduleId)
        {
            Id = Guid.NewGuid();
            CreateDateUtc = DateTime.UtcNow;
            Status = JobStatus.New;
            ModuleId = moduleId;
            _hasBeenRun = false;
            _cancellationTokenSource = new ();
            _canBeCancelled = true;
        }

        public Guid Id { get; private set; }

        public Guid ModuleId { get; private set; }

        public IMainModule MainModule { get; private set; }

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
            if (_hasBeenRun)
            {
                throw new ArgumentException($"The job can't be run anymore. Status: {Status}");
            }

            StartDateUtc = DateTime.UtcNow;
            Status = JobStatus.InProgress;

            _hasBeenRun = true;
        }

        public void Finish(double result)
        {
            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Done;
            Result = result;

            OnFinished();
        }

        public void Fail(string errorMessage)
        {
            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Error;
            ErrorMessage = errorMessage;

            OnFinished();
        }

        public void Cancel()
        {
            if (!_canBeCancelled)
            {
                return;
            }

            EndDateUtc = DateTime.UtcNow;
            Status = JobStatus.Cancelled;
            _cancellationTokenSource.Cancel();

            OnFinished();
        }

        public void SetDaemons(IEnumerable<Daemon> daemons)
        {
            CanBeExecutedOnDaemons = daemons;
        }

        public void SetMainModule(IMainModule mainModule)
        {
            MainModule = mainModule;
        }

        private void OnFinished()
        {
            _canBeCancelled = false;
            MainModule = null;

            var jobCompletionNotification = new JobCompletionNotification(Id, Status);

            foreach (var observer in _observers)
            {
                observer.OnNext(jobCompletionNotification);
            }
        }

        public IDisposable Subscribe(IObserver<JobCompletionNotification> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }

            return new Unsubscriber<JobCompletionNotification>(_observers, observer);
        }
    }
}