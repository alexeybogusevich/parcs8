namespace Parcs.HostAPI.Models.Responses
{
    public class CreateAsynchronousJobRunCommandResponse
    {
        public CreateAsynchronousJobRunCommandResponse()
        {
        }

        public CreateAsynchronousJobRunCommandResponse(Guid jobId)
        {
            JobId = jobId;
        }

        public Guid JobId { get; set; }
    }
}