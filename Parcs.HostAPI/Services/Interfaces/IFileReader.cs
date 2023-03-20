using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IFileReader
    {
        Task<IEnumerable<FileDescription>> ReadAsync(string directoryPath, CancellationToken cancellationToken = default);
        Task<FileDescription> ReadAsync(string directoryPath, string fileName, CancellationToken cancellationToken = default);
    }
}