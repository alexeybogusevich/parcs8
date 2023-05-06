namespace Parcs.Data.Entities
{
    public class JobFailureEntity
    {
        public JobFailureEntity()
        {
        }

        public JobFailureEntity(long jobId, string message, string stackTrace)
        {
            JobId = jobId;
            Message = message;
            StackTrace = stackTrace;
        }

        public long Id { get; set; }

        public long JobId { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public JobEntity Job { get; set; }
    }
}