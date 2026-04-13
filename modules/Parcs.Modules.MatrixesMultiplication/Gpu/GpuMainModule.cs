using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.MatrixesMultiplication.Gpu
{
    /// <summary>
    /// GPU-accelerated main module for matrix multiplication.
    ///
    /// Follows the same fan-out / fan-in structure as <c>ParallelMainModule</c>, but
    /// dispatches work to <see cref="GpuWorkerModule"/> instances instead of
    /// <c>ParallelWorkerModule</c>.  Each worker multiplies its sub-matrix pair on the
    /// local GPU, so the speedup compounds across both intra-node GPU parallelism and
    /// inter-node distribution.
    ///
    /// Allowed point counts are intentionally the same as the parallel CPU variant
    /// (1, 2, 4, 8) so that existing job configurations require no changes.
    /// </summary>
    public class GpuMainModule : IModule
    {
        private readonly List<int> _allowedPointsNumbers = [1, 2, 4, 8];

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.BindModuleOptions<ModuleOptions>();

            var matrixA = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);
            var matrixB = new Matrix(moduleOptions.MatrixSize, moduleOptions.MatrixSize, true);

            if (_allowedPointsNumbers.Contains(moduleOptions.PointsNumber) is false)
            {
                throw new ArgumentException(
                    $"Invalid number of points. Allowed values: {string.Join(", ", _allowedPointsNumbers)}");
            }

            var points = new IPoint[moduleOptions.PointsNumber];
            var channels = new IChannel[moduleOptions.PointsNumber];

            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                // Dispatch GPU worker — each runs on a daemon pod that has a GPU available.
                await points[i].ExecuteClassAsync<GpuWorkerModule>();
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var matrixABPairs = moduleOptions.PointsNumber switch
            {
                1 => [new(matrixA, matrixB)],
                2 => MatrixDivisioner.Divide2(matrixA, matrixB).ToArray(),
                4 => MatrixDivisioner.Divide4(matrixA, matrixB).ToArray(),
                8 => MatrixDivisioner.Divide8(matrixA, matrixB).ToArray(),
                _ => throw new NotSupportedException(),
            };

            for (int i = 0; i < matrixABPairs.Length; i++)
            {
                await channels[i].WriteObjectAsync(matrixABPairs[i].Item1);
                await channels[i].WriteObjectAsync(matrixABPairs[i].Item2);
            }

            var matrixCPairs = new List<Matrix>();

            for (int i = 0; i < channels.Length; ++i)
            {
                matrixCPairs.Add(await channels[i].ReadObjectAsync<Matrix>());
            }

            var matrixC = moduleOptions.PointsNumber switch
            {
                1 => matrixCPairs.First(),
                2 => MatrixDivisioner.Join2(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                4 => MatrixDivisioner.Join4(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                8 => MatrixDivisioner.Join8(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                _ => throw new NotSupportedException(),
            };

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds };
            await moduleInfo.OutputWriter.WriteToFileAsync(
                JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);

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
