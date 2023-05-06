namespace Parcs.Host.Models.Domain
{
    public sealed class FileDescription
    {
        public byte[] Content { get; init; }

        public string ContentType { get; init; }

        public string Filename { get; set; }
    }
}