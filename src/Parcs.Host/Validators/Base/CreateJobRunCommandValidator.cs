using FluentValidation;
using Parcs.Host.Models.Commands.Base;
using Parcs.Core.Services.Interfaces;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Parcs.Host.Validators.Base
{
    public abstract class CreateJobRunCommandValidator<TCreateJobRunCommandValidator> : AbstractValidator<TCreateJobRunCommandValidator>
        where TCreateJobRunCommandValidator : CreateJobRunCommand
    {
        private const string AssemblyExtension = "dll";

        protected CreateJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
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
                        .Must(c => BeAnExistingAssembly(c.ModuleId, c.MainModuleAssemblyName, moduleDirectoryPathBuilder))
                        .WithMessage("Assembly not found.")
                        .Must(c => BeAnExistingClass(c.ModuleId, c.MainModuleAssemblyName, c.MainModuleClassName, moduleDirectoryPathBuilder))
                        .WithMessage("Class not found in the assembly.");
                });

            RuleFor(c => c.MainModuleAssemblyName)
                .NotEmpty()
                .WithMessage("Main module's assembly name is required.");

            RuleFor(c => c.MainModuleClassName)
                .NotEmpty()
                .WithMessage("Main module's class name is required.");

            RuleFor(c => c.PointsNumber)
                .GreaterThan(0)
                .WithMessage("The number of points must be greater than zero.");
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
            var pathToSystemRuntime = Path.Combine(runtimeDirectory, "System.Runtime.dll");
            var pathToSystemPrivateCoreLib = Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll");

            var resolver = new PathAssemblyResolver(new string[] { assemblyFileName, pathToSystemRuntime, pathToSystemPrivateCoreLib });
            using var metadataLoadContext = new MetadataLoadContext(resolver);
            var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

            return assembly.GetTypes().Any(c => c.FullName == className || c.Name == className);
        }
    }
}