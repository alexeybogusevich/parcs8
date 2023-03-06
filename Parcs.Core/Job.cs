namespace Parcs.Core
{
    public class Job
    {
        public Guid Id { get; set; }

        public JobStatus Status { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public DateTime StartDateUtc { get; set; }

        public DateTime EndDateUtc { get; set; }
    }
}