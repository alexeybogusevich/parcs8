namespace Parcs.Host.Models.Responses
{
    public class GetPlainModuleQueryResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public int JobsNumber { get; set; }
    }
}