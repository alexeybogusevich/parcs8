namespace Parcs.Host.Models.Domain
{
    public sealed class JobCompletionNotification
    {
        public JobCompletionNotification(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}