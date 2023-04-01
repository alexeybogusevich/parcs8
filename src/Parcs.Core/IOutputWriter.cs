namespace Parcs.Net
{
    public interface IOutputWriter
    {
        Task WriteToFileAsync(byte[] bytes, string fileName = null);
    }
}