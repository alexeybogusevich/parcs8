namespace Parcs.Portal.Models.Host.Requests
{
    public class RunJobHostRequest
    {
        public long JobId { get; set; }

        public Dictionary<string, string> Arguments { get; set; }

        public string CallbackUrl { get; set; }
    }
}