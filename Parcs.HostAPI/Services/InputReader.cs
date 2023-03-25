using Parcs.Net;

namespace Parcs.HostAPI.Services
{
    public class InputReader : IInputReader
    {
        private readonly string _basePath;

        public InputReader(string basePath)
        {
            _basePath = basePath;
        }

        public IEnumerable<string> GetFilenames() => Directory.GetFiles(_basePath).Select(Path.GetFileName);

        public FileStream GetFileStreamForFile(string filename)
        {
            ArgumentException.ThrowIfNullOrEmpty(filename);

            var filePath = Directory
                .GetFiles(_basePath)
                .FirstOrDefault(filePath => filePath.EndsWith(filename));

            return filePath switch
            {
                null => throw new ArgumentException($"{filename} not found among the input files for the job."),
                _ => new FileStream(filePath, FileMode.Open)
            };
        }
    }
}