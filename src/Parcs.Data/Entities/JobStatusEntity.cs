namespace Parcs.Data.Entities
{
    public class JobStatusEntity
    {
        public JobStatusEntity()
        {
        }

        public JobStatusEntity(long jobId, short jobStatus)
        {
            JobId = jobId;
            Status = jobStatus;
        }

        public long Id { get; set; }

        public long JobId { get; set; }

        public short Status { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public JobEntity Job { get; set; }
    }
}