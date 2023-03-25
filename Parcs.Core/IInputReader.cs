namespace Parcs.Net
{
    public interface IInputReader
    {
        IEnumerable<string> GetFilenames();
        FileStream GetFileStreamForFile(string filename);
    }
}