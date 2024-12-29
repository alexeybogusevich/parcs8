using FluentValidation;
using Parcs.Host.Models.Commands;

namespace Parcs.Host.Validators
{
    public class RunJobAsynchronouslyCommandValidator : AbstractValidator<RunJobAsynchronouslyCommand>
    {
        public RunJobAsynchronouslyCommandValidator()
        {
            RuleFor(c => c.CallbackUrl)
                .NotEmpty()
                .WithMessage("Callback URL is required.")
                .Must(BeAValidUri)
                .WithMessage("Invalid callback URL.");
        }

        private static bool BeAValidUri(string uri)
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out _);
        }
    }
}