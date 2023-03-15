using Microsoft.Extensions.Options;
using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class InputReaderFactory : IInputReaderFactory
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public InputReaderFactory(IOptions<FileSystemConfiguration> fileSystemOptions)
        {
            _fileSystemConfiguration = fileSystemOptions.Value;
        }

        public IInputReader Create(Guid jobId) => new InputReader(_fileSystemConfiguration.InputFoldersPath, jobId);
    }
}