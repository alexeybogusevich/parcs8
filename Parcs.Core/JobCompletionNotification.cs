namespace Parcs.Core
{
    public class JobCompletionNotification
    {
        public JobCompletionNotification(Guid jobId, JobStatus jobStatus)
        {
            JobId = jobId;
            JobStatus = jobStatus;
        }

        public Guid JobId { get; set; }

        public JobStatus JobStatus { get; set; }
    }
}