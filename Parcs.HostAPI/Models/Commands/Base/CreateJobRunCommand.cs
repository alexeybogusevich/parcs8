using Parcs.Core;

namespace Parcs.HostAPI.Models.Commands.Base
{
    public class CreateJobRunCommand
    {
        public CreateJobRunCommand(
            Guid moduleId,
            string assemblyName,
            string className,
            IEnumerable<IFormFile> inputFiles,
            IEnumerable<Daemon> daemons)
        {
            ModuleId = moduleId;
            AssemblyName = assemblyName;
            ClassName = className;
            InputFiles = inputFiles;
            Daemons = daemons;
        }

        public Guid ModuleId { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}