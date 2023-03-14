namespace Parcs.Core
{
    public interface IInputReader
    {
        IEnumerable<string> GetFilenames();
        FileStream GetFileStreamForFile(string filename);
    }
}