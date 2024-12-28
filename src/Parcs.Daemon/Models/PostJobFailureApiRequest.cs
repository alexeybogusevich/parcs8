namespace Parcs.Daemon.Models
{
    public class PostJobFailureApiRequest(long jobId, string message, string stackTrace)
    {
        public long JobId { get; set; } = jobId;

        public string Message { get; set; } = message;

        public string StackTrace { get; set; } = stackTrace;
    }
}