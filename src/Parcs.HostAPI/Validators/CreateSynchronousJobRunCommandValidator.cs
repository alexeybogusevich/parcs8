using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.HostAPI.Validators.Base;

namespace Parcs.HostAPI.Validators
{
    public class CreateSynchronousJobRunCommandValidator : CreateJobRunCommandValidator<CreateSynchronousJobRunCommand>
    {
        public CreateSynchronousJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
            : base(moduleDirectoryPathBuilder)
        {
        }
    }
}