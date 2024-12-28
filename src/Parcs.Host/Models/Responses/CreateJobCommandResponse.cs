namespace Parcs.Host.Models.Responses
{
    public class CreateJobCommandResponse(long jobId)
    {
        public long JobId { get; set; } = jobId;
    }
}