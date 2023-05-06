namespace Parcs.Host.Services.Interfaces
{
    public interface IFileSaver
    {
        Task SaveAsync(IFormFile file, string directoryPath, CancellationToken cancellationToken = default);
        Task SaveAsync(IEnumerable<IFormFile> files, string directoryPath, CancellationToken cancellationToken = default);
    }
}