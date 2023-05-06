using MediatR;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public CreateJobCommand(CreateJobRunCommand jobRunCommand)
        {
            ModuleId = jobRunCommand.ModuleId;
            InputFiles = jobRunCommand.InputFiles;
            AssemblyName = jobRunCommand.MainModuleAssemblyName;
            ClassName = jobRunCommand.MainModuleClassName;
        }

        public Guid ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}