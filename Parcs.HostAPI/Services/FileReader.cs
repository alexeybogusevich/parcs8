using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class FileReader : IFileReader
    {
        public async Task<byte[]> ReadAsync(string directoryPath, string fileName, CancellationToken cancellationToken = default)
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

            await using var fileStream = File.OpenRead(filePath);
            await using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);

            return memoryStream.ToArray();
        }
    }
}