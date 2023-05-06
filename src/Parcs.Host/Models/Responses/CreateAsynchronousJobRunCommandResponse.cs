namespace Parcs.Host.Models.Responses
{
    public class CreateAsynchronousJobRunCommandResponse
    {
        public CreateAsynchronousJobRunCommandResponse()
        {
        }

        public CreateAsynchronousJobRunCommandResponse(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}