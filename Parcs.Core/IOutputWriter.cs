namespace Parcs.Core
{
    public interface IOutputWriter
    {
        Task WriteToFileAsync(byte[] bytes, string fileName = null, CancellationToken cancellationToken = default);
    }
}