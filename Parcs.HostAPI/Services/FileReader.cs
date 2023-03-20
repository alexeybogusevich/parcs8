using Microsoft.AspNetCore.StaticFiles;
using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class FileReader : IFileReader
    {
        public async Task<FileDescription> ReadAsync(string directoryPath, string fileName, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(directoryPath, fileName);

            if (!Directory.Exists(directoryPath))
            {
                throw new ArgumentException($"Directory not found: {directoryPath}");
            }

            if (!File.Exists(filePath))
            {
                throw new ArgumentException($"File not found: {fileName}");
            }

            var provider = new FileExtensionContentTypeProvider();
            _ = provider.TryGetContentType(filePath, out var contentType);

            await using var fileStream = File.OpenRead(filePath);
            await using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);

            return new FileDescription
            {
                Filename = Path.GetFileName(filePath),
                Content = memoryStream.ToArray(),
                ContentType = contentType,
            };
        }
    }
}