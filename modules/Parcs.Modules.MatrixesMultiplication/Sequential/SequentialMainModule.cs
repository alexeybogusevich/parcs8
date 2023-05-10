using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;
using System.Diagnostics;

namespace Parcs.Modules.MatrixesMultiplication.Sequential
{
    public class SequentialMainModule : IModule
    {
        public Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            var matrixA = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);
            var matrixB = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            matrixA.MultiplyBy(matrixB, cancellationToken);

            stopwatch.Stop();

            return Task.CompletedTask;
        }
    }
}