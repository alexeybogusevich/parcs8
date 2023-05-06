using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Validators.Base;
using Parcs.Core.Services.Interfaces;

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