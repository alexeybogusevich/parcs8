namespace Parcs.Core.Models
{
    public class JobMetadata
    {
        public JobMetadata(long jobId, long moduleId)
        {
            JobId = jobId;
            ModuleId = moduleId;
        }

        public long JobId { get; set; }

        public long ModuleId { get; set; }
    }
}