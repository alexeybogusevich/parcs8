using Parcs.Host.Models.Commands;
using FluentValidation;

namespace Parcs.Host.Validators
{
    public class CreateSynchronousJobRunCommandValidator : AbstractValidator<RunJobSynchronouslyCommand>
    {
        public CreateSynchronousJobRunCommandValidator()
        {
            RuleFor(c => c.PointsNumber)
                .GreaterThan(0)
                .WithMessage("The number of points must be greater than zero.");
        }
    }
}