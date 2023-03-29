using Parcs.Shared.Models;

namespace Parcs.HostAPI.Models.Commands.Base
{
    public class CreateJobRunCommand
    {
        public CreateJobRunCommand()
        {
        }

        public CreateJobRunCommand(
            Guid moduleId,
            string assemblyName,
            string className,
            IEnumerable<IFormFile> inputFiles,
            IEnumerable<Daemon> daemons)
        {
            ModuleId = moduleId;
            MainModuleAssemblyName = assemblyName;
            MainModuleClassName = className;
            InputFiles = inputFiles;
            Daemons = daemons;
        }

        public Guid ModuleId { get; set; }

        public string MainModuleAssemblyName { get; set; }

        public string MainModuleClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}