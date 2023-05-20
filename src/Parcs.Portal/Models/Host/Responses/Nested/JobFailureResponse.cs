namespace Parcs.Portal.Models.Host.Responses.Nested
{
    public class JobFailureResponse
    {
        public JobFailureResponse(string message, string stackTrace, DateTime createDateUtc)
        {
            Message = message;
            StackTrace = stackTrace;
            CreateDateUtc = createDateUtc;
        }

        public string Message { get; set; }

        public string StackTrace { get; set; }

        public DateTime CreateDateUtc { get; set; }
    }
}