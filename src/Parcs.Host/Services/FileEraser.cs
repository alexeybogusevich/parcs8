using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Services
{
    public sealed class FileEraser : IFileEraser
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