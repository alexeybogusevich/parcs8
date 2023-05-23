namespace Parcs.Host.Models.Responses
{
    public class CloneJobCommandResponse
    {
        public CloneJobCommandResponse(long id)
        {
            Id = id;
        }

        public long Id { get; set; } 
    }
}