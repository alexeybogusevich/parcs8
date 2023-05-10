using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.MatrixesMultiplication.Parallel
{
    public class ParallelMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            var matrixA = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);
            var matrixB = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);
            
            var rootPoint = await moduleInfo.CreatePointAsync();
            var rootChannel = await rootPoint.CreateChannelAsync();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await rootPoint.ExecuteClassAsync<ParallelRecursiveWorkerModule>();

            await rootChannel.WriteObjectAsync(matrixA);
            await rootChannel.WriteObjectAsync(matrixB);

            var matrixC = await rootChannel.ReadObjectAsync<Matrix>();

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);

            if (moduleOptions.SaveMatrixes)
            {
                await using var fileStreamA = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.MatrixAOutputFilename);
                await matrixA.WriteToStreamAsync(fileStreamA, cancellationToken);

                await using var fileStreamB = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.MatrixBOutputFilename);
                await matrixB.WriteToStreamAsync(fileStreamB, cancellationToken);

                await using var fileStreamC = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.MatrixCOutputFilename);
                await matrixC.WriteToStreamAsync(fileStreamC, cancellationToken);
            }
        }
    }
}