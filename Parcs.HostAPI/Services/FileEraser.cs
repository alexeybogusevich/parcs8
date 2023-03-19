using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class FileEraser : IFileEraser
    {
        public void TryDeleteRecursively(string baseDirectoryPath)
        {
            if (!Directory.Exists(baseDirectoryPath))
            {
                return;
            }

            Directory.Delete(baseDirectoryPath, true);
        }
    }
}