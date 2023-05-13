using Parcs.Net;
using System.Diagnostics;
using System.Text;

namespace Parcs.Modules.FloydWarshall.Parallel
{
    public class ParallelMainModule : IModule
    {
        private IChannel[] _channels;
        private List<List<int>> _matrix;

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            int pointsNumber = moduleInfo.ArgumentsProvider.GetPointsNumber();
            _matrix = GetMatrix(options.InputFile, moduleInfo);

            if (_matrix.Count % pointsNumber != 0)
            {
                throw new ArgumentException($"Matrix size (now {_matrix.Count}) should be divided by {pointsNumber}!");
            }

            _channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                _channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<ParallelWorkerModule>();
            }

            await DistributeAllDataAsync(pointsNumber);

            Stopwatch sw = new();
            sw.Start();
            await RunParallelFloydAsync(pointsNumber);
            sw.Stop();

            int[][] result = await GatherAllDataAsync(pointsNumber);

            await SaveMatrixAsync(options.OutputFile, result, moduleInfo);
            Console.WriteLine("Done");
            Console.WriteLine($"Total time {sw.ElapsedMilliseconds} ms ({sw.ElapsedTicks} ticks)");

            PrintMatrix(result);
        }

        static List<List<int>> GetMatrix(string filename, IModuleInfo moduleInfo)
        {
            List<string> lines = new();

            using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(filename);
            using var streamReader = new StreamReader(fileStream);

            while (!streamReader.EndOfStream)
            {
                lines.Add(streamReader.ReadLine());
            }

            return lines
                   .Select(l => l.Split(' ')
                   .Where(k => k.Length > 0)
                   .Select(i => int.Parse(i.Replace("-1", int.MaxValue.ToString())))
                   .ToList())
                   .ToList();
        }

        static async Task SaveMatrixAsync(string filename, int[][] m, IModuleInfo moduleInfo)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < m.Length; i++)
            {
                for (int j = 0; j < m.Length; j++)
                {
                    stringBuilder.Append(m[i][j]);
                    if (j != m.Length - 1)
                    {
                        stringBuilder.Append(' ');
                    }
                }

                stringBuilder.AppendLine();
            }

            await moduleInfo.OutputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes(stringBuilder.ToString()), filename);
        }

        private static void PrintMatrix(int[][] m)
        {
            int rowLength = m.Length;

            for (int i = 0; i < rowLength; i++)
            {
                for (int j = 0; j < m[i].Length; j++)
                {
                    Console.Write(m[i][j] + " ");
                }

                Console.WriteLine();
            }
        }

        private async Task<int[][]> GatherAllDataAsync(int pointsNumber)
        {
            int chunkSize = _matrix.Count / pointsNumber;

            int[][] result = new int[_matrix.Count][];

            for (int i = 0; i < _channels.Length; i++)
            {
                int[][] chunk = await _channels[i].ReadObjectAsync<int[][]>();
                for (int j = 0; j < chunkSize; j++)
                {
                    result[i * chunkSize + j] = chunk[j];
                }
            }

            return result;
        }

        private async Task RunParallelFloydAsync(int pointsNumber)
        {
            int chunkSize = _matrix.Count / pointsNumber;

            for (int k = 0; k < _matrix.Count; k++)
            {
                int currentSupplier = k / chunkSize;
                int[] currentRow = await _channels[currentSupplier].ReadObjectAsync<int[]>();

                for (int ch = 0; ch < _channels.Length; ch++)
                {
                    if (ch != currentSupplier)
                    {
                        await _channels[ch].WriteObjectAsync(currentRow);
                    }
                }
            }
        }

        private async Task DistributeAllDataAsync(int pointsNumber)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                await _channels[i].WriteDataAsync(i);

                int chunkSize = _matrix.Count / pointsNumber;

                var chunk = new List<List<int>>(chunkSize);

                for (int i = 0; i < chunkSize; ++i)
                {

                }

                for (int j = 0; j < chunkSize; j++)
                {
                    chunk[j] = _matrix[i * chunkSize + j];
                }

                await _channels[i].WriteObjectAsync(chunk);
            }
        }
    }
}