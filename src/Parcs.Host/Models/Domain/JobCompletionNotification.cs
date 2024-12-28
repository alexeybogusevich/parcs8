namespace Parcs.Host.Models.Domain
{
    public sealed class JobCompletionNotification(long jobId)
    {
        public long JobId { get; set; } = jobId;
    }
}