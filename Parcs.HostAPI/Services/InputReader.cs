using Parcs.Core;
using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services
{
    public class InputReader : IInputReader
    {
        private readonly string _targetDirectoryPath;

        public InputReader(string basePath, Guid jobId)
        {
            _targetDirectoryPath = Path.Combine(basePath, jobId.ToString(), JobDirectoryGroup.Input.ToString());
        }

        public IEnumerable<string> GetFilenames() => Directory.GetFiles(_targetDirectoryPath).Select(Path.GetFileName);

        public FileStream GetFileStreamForFile(string filename)
        {
            ArgumentException.ThrowIfNullOrEmpty(filename);

            var filePath = Directory
                .GetFiles(_targetDirectoryPath)
                .FirstOrDefault(filePath => filePath.EndsWith(filename));

            return filePath switch
            {
                null => throw new ArgumentException($"{filename} not found among the input files for the job."),
                _ => new FileStream(filePath, FileMode.Open)
            };
        }
    }
}