using FluentValidation;
using Parcs.HostAPI.Models.Queries;

namespace Parcs.HostAPI.Validators
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