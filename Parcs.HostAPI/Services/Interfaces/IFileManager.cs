using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IFileManager
    {
        Task SaveAsync(IEnumerable<IFormFile> files, DirectoryGroup directoryGroup, Guid jobId, CancellationToken cancellationToken = default);
        Task CleanAsync(Guid jobId, CancellationToken cancellationToken = default);
    }
}