using FluentValidation;
using Parcs.Host.Models.Commands;
using Parcs.Host.Validators.Base;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Host.Validators
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