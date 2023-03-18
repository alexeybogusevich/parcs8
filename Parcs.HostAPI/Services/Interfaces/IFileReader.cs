namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IFileReader
    {
        Task<byte[]> ReadAsync(string directoryPath, string fileName, CancellationToken cancellationToken = default);
    }
}