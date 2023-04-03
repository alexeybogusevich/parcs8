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
            string jsonArgumentsDictionary,
            int numberOfDaemons)
        {
            ModuleId = moduleId;
            MainModuleAssemblyName = assemblyName;
            MainModuleClassName = className;
            InputFiles = inputFiles;
            JsonArgumentsDictionary = jsonArgumentsDictionary;
            NumberOfDaemons = numberOfDaemons;
        }

        public Guid ModuleId { get; set; }

        public string MainModuleAssemblyName { get; set; }

        public string MainModuleClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string JsonArgumentsDictionary { get; set; }

        public int NumberOfDaemons { get; set; }
    }
}