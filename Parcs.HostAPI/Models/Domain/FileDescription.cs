namespace Parcs.HostAPI.Models.Domain
{
    public class FileDescription
    {
        public byte[] Content { get; init; }

        public string ContentType { get; init; }

        public string Filename { get; set; }
    }
}