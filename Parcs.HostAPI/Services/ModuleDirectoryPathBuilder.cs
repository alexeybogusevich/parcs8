using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Constants;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class ModuleDirectoryPathBuilder : IModuleDirectoryPathBuilder
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public ModuleDirectoryPathBuilder(IOptions<FileSystemConfiguration> options)
        {
            _fileSystemConfiguration = options.Value;
        }

        public string Build(Guid moduleId, ModuleDirectoryGroup directoryGroup)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Modules, moduleId.ToString(), directoryGroup.ToString());
        }
    }
}