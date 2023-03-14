namespace Parcs.HostAPI.Models.Responses
{
    public class CreateJobCommandResponse
    {
        public CreateJobCommandResponse(Guid jobId)
        {
            JobId = jobId;
        }

        public Guid JobId { get; set; }
    }
}