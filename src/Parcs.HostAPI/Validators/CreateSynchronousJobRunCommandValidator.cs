using FluentValidation;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Parcs.HostAPI.Validators
{
    public class CreateSynchronousJobRunCommandValidator : AbstractValidator<CreateSynchronousJobRunCommand>
    {
        private const string AssemblyExtension = "dll";

        public CreateSynchronousJobRunCommandValidator(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(c => c.ModuleId)
                .NotEmpty()
                .WithMessage("Module Id is required.")
                .Must(moduleId => BeExistingModule(moduleId, moduleDirectoryPathBuilder))
                .WithMessage("Module does not exist.");

            RuleFor(c => c.MainModuleAssemblyName)
                .NotEmpty()
                .WithMessage("Main module's assembly name is required.");

            RuleFor(c => c.MainModuleClassName)
                .NotEmpty()
                .WithMessage("Main module's class name is required.");

            RuleFor(c => c)
                .Must(c => BeExistingAssembly(c.ModuleId, c.MainModuleAssemblyName, moduleDirectoryPathBuilder))
                .WithMessage("Assembly not found.")
                .Must(c => BeExistingClass(c.ModuleId, c.MainModuleAssemblyName, c.MainModuleClassName, moduleDirectoryPathBuilder))
                .WithMessage("Class not found in the assembly.");
        }

        private static bool BeExistingModule(Guid moduleId, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            return Path.Exists(moduleDirectoryPathBuilder.Build(moduleId));
        }

        private static bool BeExistingAssembly(Guid moduleId, string assemblyName, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            var assemblyDirectoryPath = moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
            var assemblyPath = Path.Combine(assemblyDirectoryPath, $"{assemblyName}.{AssemblyExtension}");

            return File.Exists(assemblyPath);
        }

        private static bool BeExistingClass(Guid moduleId, string assemblyName, string className, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            var assemblyDirectoryPath = moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
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