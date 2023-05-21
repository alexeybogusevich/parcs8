using Parcs.Portal.Models;

namespace Parcs.Portal.Services.Interfaces
{
    public interface IDocumentResponseProcessor
    {
        DocumentDownloadResponse Parse(byte[] bytes, Dictionary<string, string> headers);
    }
}