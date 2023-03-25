using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;
using System.IO.Compression;

namespace Parcs.HostAPI.Services
{
    public sealed class FileArchiver : IFileArchiver
    {
        public async Task<FileDescription> ArchiveDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new ArgumentException($"Directory not found: {directoryPath}.");
            }

            var zipArchiveName = $"{new DirectoryInfo(directoryPath).Name}.zip"; 

            await using var memoryStream = new MemoryStream();
            var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create);

            foreach (var filePath in Directory.GetFiles(directoryPath))
            {
                var zipArchiveEntry = zipArchive.CreateEntry(Path.GetFileName(filePath), CompressionLevel.Fastest);
                await using var zipStream = zipArchiveEntry.Open();
                await using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(zipStream);
            }

            zipArchive.Dispose();

            return new FileDescription
            {
                Content = memoryStream.ToArray(),
                ContentType = "application/zip",
                Filename = zipArchiveName,
            };
        }
    }
}