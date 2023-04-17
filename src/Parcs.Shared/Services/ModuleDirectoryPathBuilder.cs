using Microsoft.Extensions.Options;
using Parcs.Shared.Configuration;
using Parcs.Shared.Models.Constants;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
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

        public string Build(Guid moduleId)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Modules, moduleId.ToString());
        }
    }
}