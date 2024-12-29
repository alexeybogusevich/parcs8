using Parcs.Host.Models.Commands;
using FluentValidation;

namespace Parcs.Host.Validators
{
    public class CreateSynchronousJobRunCommandValidator : AbstractValidator<RunJobSynchronouslyCommand>
    {
    }
}