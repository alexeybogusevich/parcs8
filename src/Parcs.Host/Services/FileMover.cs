using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Services
{
    public class FileMover : IFileMover
    {
        public void Copy(string fromDirectory, string toDirectory)
        {
            if (!Directory.Exists(fromDirectory))
            {
                throw new ArgumentException("Directory does not exist");
            }

            if (!Directory.Exists(toDirectory))
            {
                Directory.CreateDirectory(toDirectory);
            }

            foreach (var file in Directory.GetFiles(fromDirectory))
            {
                File.Copy(file, Path.Combine(toDirectory, Path.GetFileName(file)));
            }
        }
    }
}