using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IFileArchiver
    {
        Task<FileDescription> ArchiveDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
    }
}