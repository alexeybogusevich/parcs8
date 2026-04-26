using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Services
{
    public sealed class FileSaver : IFileSaver
    {
        public async Task SaveAsync(IFormFile file, string directoryPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                if (OperatingSystem.IsLinux())
                    File.SetUnixFileMode(directoryPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
            }

            if (file.Length <= 0)
            {
                return;
            }

            var filePath = Path.Combine(directoryPath, file.FileName);
            var streamOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                 UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                 UnixFileMode.OtherRead | UnixFileMode.OtherWrite,
            };
            await using var fileStream = new FileStream(filePath, streamOptions);
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