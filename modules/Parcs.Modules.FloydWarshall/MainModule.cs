using Parcs.Net;
using System.Diagnostics;
using System.Text;

namespace Parcs.Modules.FloydWarshall
{
    internal class MainModule : IMainModule
    {
        private IChannel[] _channels;
        private IPoint[] _points;
        private int[][] _matrix;
        private ModuleOptions _options;

        public string Name => "Floyd-Warshall Algorithm";

        public async Task RunAsync(IArgumentsProvider argumentsProvider, IHostInfo hostInfo, CancellationToken cancellationToken = default)
        {
            _options = argumentsProvider.Bind<ModuleOptions>();

            int pointsNum = _options.PointsCount;
            _matrix = GetMatrix(_options.InputFile, hostInfo);

            if (_matrix.Length % pointsNum != 0)
            {
                throw new ArgumentException($"Matrix size (now {_matrix.Length}) should be divided by {pointsNum}!");
            }

            _channels = new IChannel[pointsNum];
            _points = new IPoint[pointsNum];

            for (int i = 0; i < pointsNum; ++i)
            {
                _points[i] = await hostInfo.CreatePointAsync();
                _channels[i] = await _points[i].CreateChannelAsync();
                await _points[i].ExecuteClassAsync<WorkerModule>();
            }

            await DistributeAllDataAsync();

            Stopwatch sw = new();
            sw.Start();
            await RunParallelFloydAsync();
            sw.Stop();

            int[][] result = await GatherAllDataAsync();

            await SaveMatrixAsync(_options.OutputFile, result, hostInfo);
            Console.WriteLine("Done");
            Console.WriteLine($"Total time {sw.ElapsedMilliseconds} ms ({sw.ElapsedTicks} ticks)");

            PrintMatrix(result);
        }

        static int[][] GetMatrix(string filename, IHostInfo hostInfo)
        {
            var inputReader = hostInfo.GetInputReader();

            List<string> lines = new ();

            using var fileStream = inputReader.GetFileStreamForFile(filename);
            using var streamReader = new StreamReader(fileStream);
            while (!streamReader.EndOfStream)
            {
                lines.Add(streamReader.ReadLine());
            }

            return lines
                   .Select(l => l.Split(' ')
                   .Where(k => k.Length > 0)
                   .Select(i => int.Parse(i.Replace("-1", int.MaxValue.ToString())))
                   .ToArray())
                   .ToArray();
        }

        static async Task SaveMatrixAsync(string filename, int[][] m, IHostInfo hostInfo)
        {
            using var file = File.CreateText(filename);

            var stringBuilder = new StringBuilder();

            for (int i = 0; i < m.Length; i++)
            {
                for (int j = 0; j < m.Length; j++)
                {
                    stringBuilder.Append(m[i][j]);
                    if (j != m.Length - 1)
                    {
                        stringBuilder.Append(" ");
                    }
                }

                stringBuilder.AppendLine();
            }

            await hostInfo.GetOutputWriter().WriteToFileAsync(Encoding.UTF8.GetBytes(stringBuilder.ToString()), filename);
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

        private async Task<int[][]> GatherAllDataAsync()
        {
            int chunkSize = _matrix.Length / _options.PointsCount;

            int[][] result = new int[_matrix.Length][];

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

        private async Task RunParallelFloydAsync()
        {
            int chunkSize = _matrix.Length / _options.PointsCount;

            for (int k = 0; k < _matrix.Length; k++)
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

        private async Task DistributeAllDataAsync()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                Console.WriteLine($"Sent to channel: {i}");
                await _channels[i].WriteDataAsync(i);
                int chunkSize = _matrix.Length / _options.PointsCount;

                int[][] chunk = new int[chunkSize][];

                for (int j = 0; j < chunkSize; j++)
                {
                    chunk[j] = _matrix[i * chunkSize + j];
                }

                await _channels[i].WriteObjectAsync(chunk);
            }
        }
    }
}