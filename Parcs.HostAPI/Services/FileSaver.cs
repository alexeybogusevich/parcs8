using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public sealed class FileSaver : IFileSaver
    {
        public async Task SaveAsync(IFormFile file, string directoryPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (file.Length <= 0)
            {
                return;
            }

            var filePath = Path.Combine(directoryPath, file.FileName);
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        public async Task SaveAsync(IEnumerable<IFormFile> files, string directoryPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (files is null || !files.Any())
            {
                return;
            }

            foreach (var file in files)
            {
                await SaveAsync(file, directoryPath, cancellationToken);
            }
        }
    }
}