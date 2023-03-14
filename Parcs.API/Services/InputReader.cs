using Parcs.Core;

namespace Parcs.HostAPI.Services
{
    public class InputReader : IInputReader
    {
        private readonly string _targetDirectoryPath;

        public InputReader(string inputFoldersPath, Guid jobId)
        {
            _targetDirectoryPath = Path.Combine(inputFoldersPath, jobId.ToString());
        }

        public IEnumerable<string> GetFilenames() => Directory.GetFiles(_targetDirectoryPath).Select(Path.GetFileName);

        public FileStream GetFileStreamForFile(string filename)
        {
            ArgumentException.ThrowIfNullOrEmpty(filename);

            var filePath = Directory
                .GetFiles(_targetDirectoryPath)
                .FirstOrDefault(filePath => filePath.EndsWith(filename));

            if (filePath is null)
            {
                throw new ArgumentException($"{filename} not found among the input files for the job.");
            }

            return new FileStream(filePath, FileMode.Open);
        }
    }
}