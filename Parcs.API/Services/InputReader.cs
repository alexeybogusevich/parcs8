using Parcs.Core;

namespace Parcs.HostAPI.Services
{
    public class InputReader : IInputReader
    {
        private readonly Queue<string> _filenames;

        public InputReader(string inputFoldersPath, Guid jobId)
        {
            var targetDirectoryPath = Path.Combine(inputFoldersPath, jobId.ToString());
            var directoryFiles = Directory.GetFiles(targetDirectoryPath);
            _filenames = new Queue<string>(directoryFiles.Order());
        }

        public FileStream MoveNext()
        {
            if (!_filenames.TryDequeue(out var currentFilename))
            {
                throw new ArgumentException("No more files found.");
            }

            return new FileStream(currentFilename, FileMode.Open);
        }
    }
}