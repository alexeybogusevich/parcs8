using Parcs.Host.Models.Domain;

namespace Parcs.Host.Services.Interfaces
{
    public interface IFileReader
    {
        Task<FileDescription> ReadAsync(string directoryPath, string fileName, CancellationToken cancellationToken = default);
    }
}