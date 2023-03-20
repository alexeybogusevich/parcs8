using Parcs.HostAPI.Models.Domain;
using Parcs.HostAPI.Services.Interfaces;
using System.IO.Compression;

namespace Parcs.HostAPI.Services
{
    public class FileArchiver : IFileArchiver
    {
        public async Task<FileDescription> ArchiveDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new ArgumentException($"Directory not found: {directoryPath}.");
            }

            var zipArchiveName = $"{new DirectoryInfo(directoryPath).Name}.zip"; 

            await using var memoryStream = new MemoryStream();
            using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create);

            foreach (var filePath in Directory.GetFiles(directoryPath))
            {
                var zipArchiveEntry = zipArchive.CreateEntry(Path.GetFileName(filePath), CompressionLevel.Fastest);
                await using var zipStream = zipArchiveEntry.Open();
                var bytes = File.ReadAllBytes(filePath);
                zipStream.Write(bytes, 0, bytes.Length);
            }

            return new FileDescription
            {
                Content = memoryStream.ToArray(),
                ContentType = "application/zip",
                Filename = zipArchiveName,
            };
        }
    }
}