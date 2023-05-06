using MediatR;
using Parcs.Host.Models.Commands.Base;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public CreateJobCommand()
        {
        }

        public CreateJobCommand(CreateJobRunCommand jobRunCommand)
        {
            ModuleId = jobRunCommand.ModuleId;
            InputFiles = jobRunCommand.InputFiles;
            AssemblyName = jobRunCommand.MainModuleAssemblyName;
            ClassName = jobRunCommand.MainModuleClassName;
        }

        public long ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}