namespace Parcs.Data.Entities
{
    public class ModuleEntity
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public List<JobEntity> Jobs { get; set; }
    }
}