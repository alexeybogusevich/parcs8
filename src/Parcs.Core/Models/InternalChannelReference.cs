namespace Parcs.Core.Models
{
    public class InternalChannelReference
    {
        public InternalChannelReference()
        {
        }

        public InternalChannelReference(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }
    }
}