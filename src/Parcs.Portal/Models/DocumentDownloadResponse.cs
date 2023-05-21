namespace Parcs.Portal.Models
{
    public class DocumentDownloadResponse
    {
        public byte[] Content { get; set; }

        public string ContentType { get; set; }

        public string Filename { get; set; }
    }
}