using Parcs.HostAPI.Services;

namespace Parcs.Core
{
    public sealed class Job : IObservable<JobCompletedEvent>
    {
        private bool _hasBeenRun;
        private bool _canBeCancelled;
        private readonly List<IObserver<JobCompletedEvent>> _observers;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Job(Guid moduleId, string assemblyName, string className)
        {
            Id = Guid.NewGuid();
            CreateDateUtc = DateTime.UtcNow;
            Status = JobStatus.New;
            ModuleId = moduleId;
            AssemblyName = assemblyName;
            ClassName = className;
            _hasBeenRun = false;
            _cancellationTokenSource = new ();
            _canBeCancelled = true;
        }

        public Guid Id { get; private set; }

        public Guid ModuleId { get; private set; }

        public string AssemblyName { get; private set; }

        public string ClassName { get; private set; }

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
            Status = JobStatus.Done;
            Result = result;

            OnFinished();
        }

        public void Fail(string errorMessage)
        {
            if (Status == JobStatus.Cancelled)
            {
                return;
            }

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

        public IDisposable Subscribe(IObserver<JobCompletedEvent> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }

            return new Unsubscriber<JobCompletedEvent>(_observers, observer);
        }

        private void OnFinished()
        {
            _canBeCancelled = false;
            EndDateUtc = DateTime.UtcNow;
            MainModule = null;

            var jobCompletionNotification = new JobCompletedEvent(Id, Status);

            foreach (var observer in _observers)
            {
                observer.OnNext(jobCompletionNotification);
            }
        }
    }
}