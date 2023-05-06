namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IFileEraser
    {
        public void TryDeleteRecursively(string baseDirectoryPath);
    }
}