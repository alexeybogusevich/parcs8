using FluentValidation;
using Parcs.Host.Models.Queries;

namespace Parcs.Host.Validators
{
    public class GetJobQueryValidator : AbstractValidator<GetJobQuery>
    {
        public GetJobQueryValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(q => q.JobId)
                .NotEmpty()
                .WithMessage("Job Id is required.");
        }
    }
}