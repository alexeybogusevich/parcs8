namespace Parcs.Data.Entities
{
    public class JobEntity
    {
        public long Id { get; set; }

        public long ModuleId { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public ModuleEntity Module { get; set; }

        public List<JobStatusEntity> Statuses { get; set; }

        public List<JobFailureEntity> Failures { get; set; }
    }
}