namespace Parcs.Core.Models
{
    public class JobMetadata(long jobId, long moduleId)
    {
        public long JobId { get; set; } = jobId;

        public long ModuleId { get; set; } = moduleId;
    }
}