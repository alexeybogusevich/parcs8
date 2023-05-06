using FluentValidation;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Validators
{
    public class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
    {
        private const string AssemblyExtension = "dll";

        public CreateModuleCommandValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(c => c.Name)
                .NotEmpty()
                .WithMessage("Module name cannot be empty.");

            RuleFor(c => c.BinaryFiles)
                .NotEmpty()
                .WithMessage($"Binary files are required.")
                .Must(files => files.Any())
                .WithMessage($"Binary files are required.")
                .Must(files => files.Any(f => f.FileName.EndsWith(AssemblyExtension)))
                .WithMessage($"Binary files must contain at least one assembly.");
        }
    }
}