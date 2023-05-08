namespace Parcs.Net
{
    public interface IOutputWriter
    {
        FileStream GetStreamForFile(string fileName = null);

        Task WriteToFileAsync(byte[] bytes, string fileName = null);
    }
}