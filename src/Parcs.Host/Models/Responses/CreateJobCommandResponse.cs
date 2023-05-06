namespace Parcs.HostAPI.Models.Responses
{
    public class CreateJobCommandResponse
    {
        public CreateJobCommandResponse(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}