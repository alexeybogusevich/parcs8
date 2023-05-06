namespace Parcs.Host.Services.Interfaces
{
    public interface IFileEraser
    {
        public void TryDeleteRecursively(string baseDirectoryPath);
    }
}