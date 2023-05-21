namespace Parcs.Portal.Models.Host.Responses
{
    public class GetPlainModuleHostResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public int JobsNumber { get; set; }
    }
}