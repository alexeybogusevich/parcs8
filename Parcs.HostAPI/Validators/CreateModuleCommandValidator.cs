using FluentValidation;
using Parcs.HostAPI.Models.Commands;

namespace Parcs.HostAPI.Validators
{
    public class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
    {
        public CreateModuleCommandValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;
            ClassLevelCascadeMode = CascadeMode.Stop;
        }
    }
}