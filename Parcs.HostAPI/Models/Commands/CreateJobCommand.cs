using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public CreateJobCommand(CreateSynchronousJobRunCommand command)
        {
            ModuleId = command.ModuleId;
            InputFiles = command.InputFiles;
            AssemblyName = command.AssemblyName;
            ClassName = command.ClassName;
        }

        public CreateJobCommand(CreateAsynchronousJobRunCommand command)
        {
            ModuleId = command.ModuleId;
            InputFiles = command.InputFiles;
            AssemblyName = command.AssemblyName;
            ClassName = command.ClassName;
        }

        public Guid ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}