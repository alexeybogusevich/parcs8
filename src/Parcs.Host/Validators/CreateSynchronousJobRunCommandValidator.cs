using Parcs.Host.Models.Commands;
using Parcs.Host.Validators.Base;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Host.Validators
{
    public class CreateSynchronousJobRunCommandValidator : CreateJobRunCommandValidator<CreateSynchronousJobRunCommand>
    {
        public CreateSynchronousJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
            : base(moduleDirectoryPathBuilder)
        {
        }
    }
}