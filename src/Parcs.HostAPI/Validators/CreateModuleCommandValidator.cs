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

            RuleFor(c => c.WorkerBinaryFiles)
                .NotEmpty()
                .WithMessage($"Worker binary files are required.")
                .Must(files => files.Any())
                .WithMessage($"Worker binary files are required.")
                .Must(files => files.Any(f => f.FileName.EndsWith(AssemblyExtension)))
                .WithMessage($"Worker binary files must contain at least one assembly.");

            RuleFor(c => c.HostBinaryFiles)
                .NotEmpty()
                .WithMessage($"Host binary files are required.")
                .Must(files => files.Any())
                .WithMessage($"Host binary files are required.")
                .Must(files => files.Any(f => f.FileName.EndsWith(AssemblyExtension)))
                .WithMessage($"Host binary files must contain at least one assembly.");
        }
    }
}