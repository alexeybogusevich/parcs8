namespace Parcs.Host.Models.Commands.Base
{
    public class CreateJobRunCommand
    {
        public CreateJobRunCommand()
        {
        }

        public CreateJobRunCommand(
            long moduleId,
            string assemblyName,
            string className,
            IEnumerable<IFormFile> inputFiles,
            int pointsNumber,
            Dictionary<string, string> arguments)
        {
            ModuleId = moduleId;
            MainModuleAssemblyName = assemblyName;
            MainModuleClassName = className;
            InputFiles = inputFiles;
            PointsNumber = pointsNumber;
            Arguments = arguments;
        }

        public long ModuleId { get; set; }

        public string MainModuleAssemblyName { get; set; }

        public string MainModuleClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public int PointsNumber { get; set; }

        public Dictionary<string, string> Arguments { get; set; }
    }
}