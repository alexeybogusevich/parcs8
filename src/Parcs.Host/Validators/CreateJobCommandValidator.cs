using FluentValidation;
using Parcs.Core.Services.Interfaces;
using Parcs.Host.Models.Commands;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Parcs.Host.Validators
{
    public class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
    {
        private const string AssemblyExtension = "dll";

        public CreateJobCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(c => c.ModuleId)
                .NotEmpty()
                .WithMessage("Module Id is required.")
                .Must(moduleId => BeAnExistingModule(moduleId, moduleDirectoryPathBuilder))
                .WithMessage("Module does not exist.")
                .DependentRules(() =>
                {
                    RuleFor(c => c)
                        .Must(c => BeAnExistingAssembly(c.ModuleId, c.AssemblyName, moduleDirectoryPathBuilder))
                        .WithMessage("Assembly not found.")
                        .Must(c => BeAnExistingClass(c.ModuleId, c.AssemblyName, c.ClassName, moduleDirectoryPathBuilder))
                        .WithMessage("Class not found in the assembly.");
                });

            RuleFor(c => c.AssemblyName)
                .NotEmpty()
                .WithMessage("Module's assembly name is required.");

            RuleFor(c => c.ClassName)
                .NotEmpty()
                .WithMessage("Module's class name is required.");
        }

        private static bool BeAnExistingModule(long moduleId, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            return Path.Exists(moduleDirectoryPathBuilder.Build(moduleId));
        }

        private static bool BeAnExistingAssembly(long moduleId, string assemblyName, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            var assemblyDirectoryPath = moduleDirectoryPathBuilder.Build(moduleId);
            var assemblyPath = Path.Combine(assemblyDirectoryPath, $"{assemblyName}.{AssemblyExtension}");

            return File.Exists(assemblyPath);
        }

        private static bool BeAnExistingClass(long moduleId, string assemblyName, string className, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            var assemblyDirectoryPath = moduleDirectoryPathBuilder.Build(moduleId);
            var assemblyFileName = $"{assemblyName}.{AssemblyExtension}";
            var assemblyPath = Path.Combine(assemblyDirectoryPath, assemblyFileName);

            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

            // Include DLLs from the module directory, the host app directory (provides Parcs.Net etc.),
            // and the core runtime so all transitive dependencies can be resolved by MetadataLoadContext.
            var resolverPaths = Directory.GetFiles(assemblyDirectoryPath, $"*.{AssemblyExtension}")
                .Concat(Directory.GetFiles(AppContext.BaseDirectory, $"*.{AssemblyExtension}"))
                .Concat(Directory.GetFiles(runtimeDirectory, $"*.{AssemblyExtension}"))
                .Distinct();

            var resolver = new PathAssemblyResolver(resolverPaths);
            using var metadataLoadContext = new MetadataLoadContext(resolver);

            try
            {
                var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);
                return assembly.GetTypes().Any(c => c.FullName == className || c.Name == className);
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Partial type load — check whatever types were loaded successfully.
                return ex.Types.Where(t => t is not null)
                    .Any(t => t!.FullName == className || t.Name == className);
            }
            catch
            {
                return false;
            }
        }
    }
}