using Parcs.Net;

namespace Parcs.Core.Services
{
    public sealed class OutputWriter : IOutputWriter
    {
        private readonly string _basePath;
        private readonly CancellationToken _cancellationToken;

        public OutputWriter(string basePath, CancellationToken cancellationToken)
        {
            _basePath = basePath;
            _cancellationToken = cancellationToken;

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
        }

        public async Task WriteToFileAsync(byte[] bytes, string fileName = null)
        {
            var filePath = Path.Combine(_basePath, fileName ?? Guid.NewGuid().ToString());

            using var memoryStream = new MemoryStream(bytes);
            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await memoryStream.CopyToAsync(fileStream, _cancellationToken);
        }
    }
}