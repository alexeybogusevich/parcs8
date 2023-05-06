using Parcs.Host.Models.Domain;

namespace Parcs.Host.Services.Interfaces
{
    public interface IFileArchiver
    {
        Task<FileDescription> ArchiveDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
    }
}