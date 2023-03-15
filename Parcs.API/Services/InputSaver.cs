using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class InputSaver : IInputSaver
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public InputSaver(IOptions<FileSystemConfiguration> options)
        {
            _fileSystemConfiguration = options.Value;
        }

        public async Task SaveAsync(IEnumerable<IFormFile> inputFiles, Guid jobId, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_fileSystemConfiguration.InputFoldersPath))
            {
                Directory.CreateDirectory(_fileSystemConfiguration.InputFoldersPath);
            }

            var jobDirectoryPath = Path.Combine(_fileSystemConfiguration.InputFoldersPath, jobId.ToString());
            Directory.CreateDirectory(jobDirectoryPath);

            if (inputFiles is null || !inputFiles.Any())
            {
                return;
            }

            foreach (var file in inputFiles)
            {
                if (file.Length <= 0)
                {
                    continue;
                }

                var filePath = Path.Combine(jobDirectoryPath, file.FileName);
                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(fileStream, cancellationToken);
            }
        }
    }
}