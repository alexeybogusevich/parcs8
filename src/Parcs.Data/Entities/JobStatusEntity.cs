namespace Parcs.Data.Entities
{
    public class JobStatusEntity
    {
        public long Id { get; set; }

        public long JobId { get; set; }

        public short JobStatus { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public JobEntity Job { get; set; }
    }
}