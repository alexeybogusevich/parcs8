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

            RuleFor(c => c.BinaryFiles)
                .NotEmpty()
                .WithMessage($"Host binary files are required.")
                .Must(files => files.Any())
                .WithMessage($"Host binary files are required.")
                .Must(files => files.Any(f => f.FileName.EndsWith(AssemblyExtension)))
                .WithMessage($"Host binary files must contain at least one assembly.");
        }
    }
}