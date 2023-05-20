using Parcs.Modules.FloydWarshall.Extensions;
using Parcs.Modules.FloydWarshall.Models;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.FloydWarshall.Parallel
{
    public class ParallelMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            var initialMatrix = GetInitialDistancesMatrix(moduleInfo, moduleOptions);

            var pointsNumber = moduleInfo.ArgumentsProvider.GetPointsNumber();

            if (initialMatrix.Height % pointsNumber != 0)
            {
                throw new ArgumentException($"Matrix size (now {initialMatrix.Height}) should be divided by {pointsNumber}");
            }

            var chunkSize = initialMatrix.Height / pointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<ParallelWorkerModule>();
            }

            await DistributeChunksAsync(initialMatrix, chunkSize, channels);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await RunFloydWarshallAsync(initialMatrix, chunkSize, channels);

            stopwatch.Stop();

            var finalMatrix = await GetFinalDistancesMatrixAsync(initialMatrix, chunkSize, channels);

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFile);

            if (moduleOptions.SaveMatrixes)
            {
                await using var fileStream = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.OutputFile);
                await finalMatrix.WriteToStreamAsync(fileStream, cancellationToken);
            }
        }

        private static Matrix GetInitialDistancesMatrix(IModuleInfo moduleInfo, ModuleOptions moduleOptions)
        {
            Matrix initialMatrix;

            if (moduleOptions.InputFile is not null)
            {
                using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(moduleOptions.InputFile);
                initialMatrix = Matrix.LoadFromStream(fileStream);
            }
            else
            {
                initialMatrix = new Matrix(moduleOptions.VerticesNumber, moduleOptions.VerticesNumber, true);
                initialMatrix.FillWithRandomDistances(maxDistance: 100);
            }

            return initialMatrix;
        }

        private static async Task DistributeChunksAsync(Matrix initialMatrix, int chunkSize, IChannel[] channels)
        {
            for (int i = 0; i < channels.Length; i++)
            {
                await channels[i].WriteDataAsync(i);

                var chunk = new Matrix(chunkSize, initialMatrix.Width);

                for (int j = 0; j < chunkSize; j++)
                {
                    chunk.Data[j] = initialMatrix.Data[i * chunkSize + j];
                }

                await channels[i].WriteObjectAsync(chunk);
            }
        }

        private static async Task RunFloydWarshallAsync(Matrix initialMatrix, int chunkSize, IChannel[] channels)
        {
            for (int i = 0; i < initialMatrix.Height; ++i)
            {
                var currentRowSupplier = i / chunkSize;

                var currentRow = await channels[currentRowSupplier].ReadObjectAsync<List<int>>();

                for (int j = 0; j < channels.Length; j++)
                {
                    if (j != currentRowSupplier)
                    {
                        await channels[j].WriteObjectAsync(currentRow);
                    }
                }
            }
        }

        private static async Task<Matrix> GetFinalDistancesMatrixAsync(Matrix initialMatrix, int chunkSize, IChannel[] channels)
        {
            var finalMatrix = new Matrix(initialMatrix.Height, initialMatrix.Width);

            for (int i = 0; i < channels.Length; i++)
            {
                var chunk = await channels[i].ReadObjectAsync<Matrix>();
                for (int j = 0; j < chunkSize; j++)
                {
                    finalMatrix.Data[i * chunkSize + j] = chunk.Data[j];
                }
            }

            return finalMatrix;
        }
    }
}