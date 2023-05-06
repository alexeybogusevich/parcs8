namespace Parcs.Core.Models
{
    public class JobMetadata
    {
        public JobMetadata(Guid jobId, Guid moduleId)
        {
            JobId = jobId;
            ModuleId = moduleId;
        }

        public Guid JobId { get; set; }

        public Guid ModuleId { get; set; }
    }
}