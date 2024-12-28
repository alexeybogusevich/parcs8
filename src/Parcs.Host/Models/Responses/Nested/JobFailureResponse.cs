namespace Parcs.Host.Models.Responses.Nested
{
    public class JobFailureResponse(string message, string stackTrace, DateTime createDateUtc)
    {
        public string Message { get; set; } = message;

        public string StackTrace { get; set; } = stackTrace;

        public DateTime CreateDateUtc { get; set; } = createDateUtc;
    }
}