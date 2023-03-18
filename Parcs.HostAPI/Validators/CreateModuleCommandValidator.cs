using FluentValidation;
using Parcs.HostAPI.Models.Commands;
using System.Reflection;

namespace Parcs.HostAPI.Validators
{
    public class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
    {
        public CreateModuleCommandValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;
            ClassLevelCascadeMode = CascadeMode.Stop;
        }

        private bool ImplementIModuleInterface(IFormFile file, string className)
        {
            using var fileStream = file.OpenReadStream();

            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);

            try
            {
                var assembly = Assembly.Load(memoryStream.ToArray());
                var type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));

                if ()
            }
            catch
            {
                return false;
            }
        }
    }
}