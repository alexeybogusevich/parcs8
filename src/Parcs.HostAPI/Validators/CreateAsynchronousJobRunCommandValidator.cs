using FluentValidation;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Validators.Base;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.HostAPI.Validators
{
    public class CreateAsynchronousJobRunCommandValidator : CreateJobRunCommandValidator<CreateAsynchronousJobRunCommand>
    {
        public CreateAsynchronousJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
            : base(moduleDirectoryPathBuilder)
        {
            RuleFor(c => c.CallbackUri)
                .NotEmpty()
                .WithMessage("Callback URI is required.")
                .Must(BeAValidUri)
                .WithMessage("Invalid callback URI.");
        }

        private static bool BeAValidUri(string uri)
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out _);
        }
    }
}