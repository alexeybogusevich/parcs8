using Parcs.Portal.Constants;
using Parcs.Portal.Models;
using Parcs.Portal.Services.Interfaces;
using System.Net.Mime;

namespace Parcs.Portal.Services
{
    public class DocumentResponseProcessor : IDocumentResponseProcessor
    {
        public DocumentDownloadResponse Parse(byte[] bytes, Dictionary<string, string> headers)
        {
            var downloadResponse = new DocumentDownloadResponse
            {
                Content = bytes,
            };

            if (headers.TryGetValue(ResponseHeaders.ContentType, out var contentTypeHeaderValue))
            {
                downloadResponse.ContentType = contentTypeHeaderValue;
            }

            if (headers.TryGetValue(ResponseHeaders.ContentDisposition, out var contentDispositionHeaderValue))
            {
                var contentDisposition = new ContentDisposition(contentDispositionHeaderValue);
                downloadResponse.Filename = contentDisposition.FileName;
            }

            return downloadResponse;
        }
    }
}