namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInputWriter
    {
        Task WriteAllAsync(IEnumerable<IFormFile> inputFiles, Guid jobId, CancellationToken cancellationToken = default);
    }
}