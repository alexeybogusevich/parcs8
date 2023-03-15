using Flurl.Http;
using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class FileManager : IFileManager
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public FileManager(IOptions<FileSystemConfiguration> options)
        {
            _fileSystemConfiguration = options.Value;
        }

        public async Task SaveAsync(IEnumerable<IFormFile> files, DirectoryGroup directoryGroup, Guid jobId, CancellationToken cancellationToken = default)
        {
            var targetDirectory = Path.Combine(_fileSystemConfiguration.BasePath, jobId.ToString(), directoryGroup.ToString());

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (files is null || !files.Any())
            {
                return;
            }

            foreach (var file in files)
            {
                if (file.Length <= 0)
                {
                    continue;
                }

                var filePath = Path.Combine(targetDirectory, file.FileName);
                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(fileStream, cancellationToken);
            }
        }

        public async Task CleanAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            var jobDirectory = Path.Combine(_fileSystemConfiguration.BasePath, jobId.ToString());

            if (!Directory.Exists(jobDirectory))
            {
                return;
            }

            foreach (var folderGroup in Enum.GetValues<DirectoryGroup>())
            {
                var targetDirectory = Path.Combine(jobDirectory, folderGroup.ToString());

                if (!Directory.Exists(targetDirectory))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(targetDirectory))
                {
                    await file.DeleteAsync(CancellationToken.None);
                }

                Directory.Delete(targetDirectory);
            }

            Directory.Delete(jobDirectory);
        }
    }
}