namespace Parcs.Host.Models.Responses
{
    public class CloneJobCommandResponse(long id)
    {
        public long Id { get; set; } = id;
    }
}