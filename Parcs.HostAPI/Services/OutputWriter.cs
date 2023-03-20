using Parcs.Core;

namespace Parcs.HostAPI.Services
{
    public class OutputWriter : IOutputWriter
    {
        private readonly string _basePath;

        public OutputWriter(string basePath)
        {
            _basePath = basePath;

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
        }

        public async Task WriteToFileAsync(byte[] bytes, string fileName = null, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_basePath, fileName ?? Guid.NewGuid().ToString());

            using var memoryStream = new MemoryStream(bytes);
            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await memoryStream.CopyToAsync(fileStream, cancellationToken);
        }
    }
}