using Parcs.Modules.FloydWarshall.Extensions;
using Parcs.Modules.FloydWarshall.Models;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.FloydWarshall.Sequential
{
    public class SequentialMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.BindModuleOptions<ModuleOptions>();

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

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var finalMatrix = Floyd(initialMatrix);

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFile);

            if (moduleOptions.SaveMatrixes)
            {
                await using var fileStream = moduleInfo.OutputWriter.GetStreamForFile(moduleOptions.OutputFile);
                await finalMatrix.WriteToStreamAsync(fileStream, cancellationToken);
            }
        }

        private static Matrix Floyd(Matrix matrix)
        {
            var result = new Matrix(matrix);

            for (int k = 0; k < result.Height; k++)
            {
                for (int i = 0; i < result.Width; i++)
                {
                    for (int j = 0; j < result.Height; j++)
                    {
                        result[i, j] = MinWeight(result[i, j], result[i, k], result[k, j]);
                    }
                }
            }

            return result;
        }

        private static int MinWeight(int a, int b, int c)
        {
            if (a != int.MaxValue)
            {
                if (b != int.MaxValue && c != int.MaxValue)
                    return Math.Min(a, b + c);
                else
                    return a;
            }
            else
            {
                if (b == int.MaxValue || c == int.MaxValue)
                    return a;
                else
                    return b + c;
            }
        }
    }
}