namespace Parcs.Host.Services.Interfaces
{
    public interface IFileMover
    {
        void Copy(string fromDirectory, string toDirectory);
    }
}