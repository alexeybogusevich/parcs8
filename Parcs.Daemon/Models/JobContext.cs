namespace Parcs.Daemon.Models
{
    public class JobContext
    {
        public JobContext(Guid jobId, string workerModulesPath)
        {
            JobId = jobId;
            WorkerModulesPath = workerModulesPath;
            CancellationTokenSource = new();
        }

        public Guid JobId { get; init; }
        
        public CancellationTokenSource CancellationTokenSource { get; init; }

        public string WorkerModulesPath { get; init; }
    }
}