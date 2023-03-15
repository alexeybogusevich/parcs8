namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInputSaver
    {
        Task SaveAsync(IEnumerable<IFormFile> inputFiles, Guid jobId, CancellationToken cancellationToken = default);
    }
}