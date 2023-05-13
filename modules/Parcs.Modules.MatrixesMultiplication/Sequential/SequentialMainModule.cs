using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.MatrixesMultiplication.Sequential
{
    public class SequentialMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            var matrixA = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);
            var matrixB = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            matrixA.MultiplyBy(matrixB, cancellationToken);

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);

            if (moduleOptions.SaveMatrixes)
            {
                await using var fileStreamC = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.MatrixCOutputFilename);
                await matrixA.WriteToStreamAsync(fileStreamC, cancellationToken);
            }
        }
    }
}