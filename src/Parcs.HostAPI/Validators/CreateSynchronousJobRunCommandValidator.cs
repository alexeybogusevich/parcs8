using FluentValidation;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Validators
{
    public class CreateSynchronousJobRunCommandValidator : AbstractValidator<CreateSynchronousJobRunCommand>
    {
        public CreateSynchronousJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(c => c.ModuleId)
                .NotEmpty()
                .WithMessage("Module Id is required.")
                .Must(moduleId => Path.Exists(moduleDirectoryPathBuilder.Build(moduleId)))
                .WithMessage("Module does not exist.");

            RuleFor(c => c.MainModuleAssemblyName)
                .NotEmpty()
                .WithMessage("Main module's assembly name is required.");

            RuleFor(c => c.MainModuleClassName)
                .NotEmpty()
                .WithMessage("Main module's class name is required.");
        }
    }
}