namespace Parcs.Daemon.Models
{
    public class PostJobFailureApiRequest
    {
        public PostJobFailureApiRequest(long jobId, string message, string stackTrace)
        {
            JobId = jobId;
            Message = message;
            StackTrace = stackTrace;
        }

        public long JobId { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }
    }
}