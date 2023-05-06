using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models.Constants;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class ModuleDirectoryPathBuilder : IModuleDirectoryPathBuilder
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public ModuleDirectoryPathBuilder(IOptions<FileSystemConfiguration> options)
        {
            _fileSystemConfiguration = options.Value;
        }

        public string Build()
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Modules);
        }

        public string Build(long moduleId)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Modules, moduleId.ToString());
        }
    }
}