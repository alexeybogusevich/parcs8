using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;
using System.Text;

namespace Parcs.Modules.MatrixesMultiplication
{
    public class MainModule : IMainModule
    {
        public string Name => "Main Matrixes Multiplication Module";

        public async Task RunAsync(IHostInfo hostInfo, IInputReader inputReader, IOutputWriter outputWriter)
        {
            Matrix a, b;

            var files = inputReader.GetFilenames().ToList();

            try
            {
                a = Matrix.LoadFromStream(inputReader.GetFileStreamForFile(files[0]));
                b = Matrix.LoadFromStream(inputReader.GetFileStreamForFile(files[1]));
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"File with a given fileName {ex.FileName} not found, stopping the application...");
                return;
            }

            int[] possibleValues = { 1, 2, 4, 8, 16, 32 };

            int pointsNumber = hostInfo.AvailablePointsNumber;

            if (!possibleValues.Contains(pointsNumber))
            {
                Console.WriteLine("Cannot start module with given number of points. Possible usages: {0}", string.Join(" ", possibleValues));
                return;
            }

            Console.WriteLine("Starting Matrixes Module on {0} points", pointsNumber);

            var points = new IPoint[pointsNumber];
            var channels = new IChannel[pointsNumber];
            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            var resMatrix = new Matrix(a.Height, b.Width);
            DateTime time = DateTime.Now;
            Console.WriteLine("Waiting for a result...");

            switch (pointsNumber)
            {
                case 1:
                    await channels[0].WriteObjectAsync(a);
                    await channels[0].WriteObjectAsync(b);
                    resMatrix = await channels[0].ReadObjectAsync<Matrix>();
                    break;
                case 2:
                    {
                        var matrixPairs = Divide2(a, b).ToArray();

                        await channels[0].WriteObjectAsync(matrixPairs[0].Item1);
                        await channels[0].WriteObjectAsync(matrixPairs[0].Item2);
                        await channels[1].WriteObjectAsync(matrixPairs[1].Item1);
                        await channels[1].WriteObjectAsync(matrixPairs[1].Item2);

                        LogSendingTime(time);

                        var channelTasks = channels.Select(c => c.ReadObjectAsync<Matrix>());
                        await Task.WhenAll(channelTasks);

                        Join2(resMatrix, channelTasks.Select(ct => ct.Result).ToList());
                    }
                    break;
                case 4:
                    {
                        var matrixPairs = Divide4(a, b).ToArray();
                        for (int i = 0; i < matrixPairs.Length; i++)
                        {
                            await channels[i].WriteObjectAsync(matrixPairs[i].Item1);
                            await channels[i].WriteObjectAsync(matrixPairs[i].Item2);
                        }

                        LogSendingTime(time);

                        var channelTasks = channels.Select(c => c.ReadObjectAsync<Matrix>());
                        await Task.WhenAll(channelTasks);

                        Join4(resMatrix, channelTasks.Select(ct => ct.Result).ToList());
                    }
                    break;
                case 8:
                    {
                        var matrixPairs = Divide8(a, b).ToArray();
                        for (int i = 0; i < matrixPairs.Length; i++)
                        {
                            await channels[i].WriteObjectAsync(matrixPairs[i].Item1);
                            await channels[i].WriteObjectAsync(matrixPairs[i].Item2);
                        }

                        LogSendingTime(time);

                        var channelTasks = channels.Select(c => c.ReadObjectAsync<Matrix>());
                        await Task.WhenAll(channelTasks);

                        Join8(resMatrix, channelTasks.Select(ct => ct.Result).ToList());
                    }
                    break;
                default:
                    Console.WriteLine("Unexpected error.");
                    return;
            }

            await SaveMatrixAsync(resMatrix, outputWriter);

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }
        }

        private static void LogSendingTime(DateTime time)
        {
            Console.WriteLine("Sending finished: time = {0}", Math.Round((DateTime.Now - time).TotalSeconds, 3));
        }

        private static IEnumerable<Tuple<Matrix, Matrix>> Divide2(Matrix a, Matrix b)
        {
            yield return Tuple.Create(a.SubMatrix(0, 0, b.Height / 2, b.Width), b);
            yield return Tuple.Create(a.SubMatrix(0, 0, a.Height / 2 + a.Height % 2, a.Width), b);
        }

        private static IEnumerable<Tuple<Matrix, Matrix>> Divide4(Matrix a, Matrix b)
        {
            yield return Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width), b.SubMatrix(0, 0, b.Height, b.Width / 2));
            yield return Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width), b.SubMatrix(0, b.Width / 2, b.Height, b.Width / 2 + b.Width % 2));
            yield return Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, b.Width), b.SubMatrix(0, 0, b.Height, b.Width / 2));
            yield return Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, b.Width), b.SubMatrix(0, b.Width / 2, b.Height, b.Width / 2 + b.Width % 2));
        }

        private static IEnumerable<Tuple<Matrix, Matrix>> Divide8(Matrix a, Matrix b)
        {
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width / 2), b.SubMatrix(0, 0, b.Height / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, a.Width / 2, a.Height / 2, a.Width / 2 + a.Width % 2),
                    b.SubMatrix(b.Height / 2, 0, b.Height / 2 + b.Height % 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width / 2),
                    b.SubMatrix(0, b.Width / 2, b.Height / 2, b.Width / 2 + b.Width % 2));
            yield return Tuple.Create(a.SubMatrix(0, a.Width / 2, a.Height / 2, a.Width / 2 + a.Width % 2),
                b.SubMatrix(b.Height / 2, b.Width / 2, b.Height / 2 + b.Height % 2, b.Width / 2 + b.Width % 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, a.Width / 2),
                    b.SubMatrix(0, 0, b.Height / 2, b.Width / 2));
            yield return Tuple.Create(a.SubMatrix(a.Height / 2, a.Width / 2, a.Height / 2 + a.Height % 2,
                a.Width / 2 + a.Width % 2), b.SubMatrix(b.Height / 2, 0, b.Height / 2 + b.Height % 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, a.Width / 2),
                    b.SubMatrix(0, b.Width / 2, b.Height / 2, b.Width / 2 + b.Width % 2));
            yield return Tuple.Create(a.SubMatrix(a.Height / 2, a.Width / 2, a.Height / 2 + a.Height % 2,
                a.Width / 2 + a.Width % 2), b.SubMatrix(b.Height / 2, b.Width / 2, b.Height / 2 + b.Height % 2,
                    b.Width / 2 + b.Width % 2));
        }

        private static void Join2(Matrix resMatrix, IList<Matrix> matrixes)
        {
            resMatrix.FillSubMatrix(matrixes[0], 0, 0);
            resMatrix.FillSubMatrix(matrixes[1], (resMatrix.Height / 2), 0);
        }

        private static void Join4(Matrix resMatrix, IList<Matrix> matrixes)
        {
            resMatrix.FillSubMatrix(matrixes[0], 0, 0);
            resMatrix.FillSubMatrix(matrixes[1], 0, resMatrix.Width / 2);
            resMatrix.FillSubMatrix(matrixes[2], resMatrix.Height / 2, 0);
            resMatrix.FillSubMatrix(matrixes[3], resMatrix.Height / 2, resMatrix.Width / 2);
        }

        private static void Join8(Matrix resMatrix, IList<Matrix> matrixes)
        {
            var parts = new Matrix[2, 2];

            parts[0, 0] = matrixes[0];
            parts[0, 0].Add(matrixes[1]);
            resMatrix.FillSubMatrix(parts[0, 0], 0, 0);
            parts[0, 1] = matrixes[2];
            parts[0, 1].Add(matrixes[3]);
            resMatrix.FillSubMatrix(parts[0, 1], 0, resMatrix.Width / 2);
            parts[1, 0] = matrixes[4];
            parts[1, 0].Add(matrixes[5]);
            resMatrix.FillSubMatrix(parts[1, 0], resMatrix.Height / 2, 0);
            parts[1, 1] = matrixes[6];
            parts[1, 1].Add(matrixes[7]);
            resMatrix.FillSubMatrix(parts[1, 1], resMatrix.Height / 2, resMatrix.Width / 2);
        }

        private static Task SaveMatrixAsync(Matrix resMatrix, IOutputWriter outputWriter)
        {
            return outputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes(resMatrix.ToString()), "Result.txt");
        }
    }
}