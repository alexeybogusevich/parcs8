namespace Parcs.Portal.Models.Host.Responses
{
    public class CloneJobHostResponse
    {
        public CloneJobHostResponse(long id)
        {
            Id = id;
        }

        public long Id { get; set; }
    }
}