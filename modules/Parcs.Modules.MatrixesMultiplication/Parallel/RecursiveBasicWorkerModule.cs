using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Parallel
{
    public class RecursiveBasicWorkerModule : IModule
    {
        private readonly object _lockerA = new();
        private readonly object _lockerB = new();

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            lock (_lockerA)
            {
                Console.WriteLine($"Matrix A Height: {matrixA.Height}, Width: {matrixA.Width}.");
                Console.WriteLine("Matrix A:");
                Console.WriteLine(matrixA.ToString());

                Console.WriteLine($"Matrix B Height: {matrixB.Height}, Width: {matrixB.Width}.");
                Console.WriteLine("Matrix B:");
                Console.WriteLine(matrixB.ToString());
            }

            var points = new IPoint[8];
            var channels = new IChannel[8];

            for (int i = 0; i < 8; i++)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();

                if (matrixA.Width > 4)
                {
                    await points[i].ExecuteClassAsync<RecursiveBasicWorkerModule>();
                }
                else
                {
                    await points[i].ExecuteClassAsync<AtomicWorkerModule>();
                }
            }

            await channels[0].WriteObjectAsync(matrixA.SubMatrix(0, 0, matrixA.Width / 2, matrixA.Width / 2)); // A11
            await channels[0].WriteObjectAsync(matrixB.SubMatrix(0, 0, matrixB.Width / 2, matrixB.Width / 2)); // B11

            await channels[1].WriteObjectAsync(matrixA.SubMatrix(0, matrixA.Width / 2, matrixA.Width / 2, matrixA.Width / 2)); //A12
            await channels[1].WriteObjectAsync(matrixB.SubMatrix(matrixB.Width / 2, 0, matrixB.Width / 2, matrixB.Width / 2)); //B21

            await channels[2].WriteObjectAsync(matrixA.SubMatrix(0, 0, matrixA.Width / 2, matrixA.Width / 2));
            await channels[2].WriteObjectAsync(matrixB.SubMatrix(0, matrixB.Width / 2, matrixB.Width / 2, matrixB.Width / 2));

            await channels[3].WriteObjectAsync(matrixA.SubMatrix(0, matrixA.Width / 2, matrixA.Width / 2, matrixA.Width / 2));
            await channels[3].WriteObjectAsync(matrixB.SubMatrix(matrixB.Width / 2, matrixB.Width / 2, matrixB.Width / 2,
                matrixB.Width / 2));

            await channels[4].WriteObjectAsync(matrixA.SubMatrix(matrixA.Width / 2, 0, matrixA.Width / 2, matrixA.Width / 2));
            await channels[4].WriteObjectAsync(matrixB.SubMatrix(0, 0, matrixB.Width / 2, matrixB.Width / 2));

            await channels[5].WriteObjectAsync(matrixA.SubMatrix(matrixA.Width / 2, matrixA.Width / 2, matrixA.Width / 2,
                matrixA.Width / 2));
            await channels[5].WriteObjectAsync(matrixB.SubMatrix(matrixB.Width / 2, 0, matrixB.Width / 2, matrixB.Width / 2));

            await channels[6].WriteObjectAsync(matrixA.SubMatrix(matrixA.Width / 2, 0, matrixA.Width / 2, matrixA.Width / 2));
            await channels[6].WriteObjectAsync(matrixB.SubMatrix(0, matrixB.Width / 2, matrixB.Width / 2, matrixB.Width / 2));

            await channels[7].WriteObjectAsync(matrixA.SubMatrix(matrixA.Width / 2, matrixA.Width / 2, matrixA.Width / 2,
                matrixA.Width / 2));
            await channels[7].WriteObjectAsync(matrixB.SubMatrix(matrixB.Width / 2, matrixB.Width / 2, matrixB.Width / 2,
                matrixB.Width / 2));

            var resultMatrix = new Matrix(matrixA.Height, matrixA.Height);
            resultMatrix.SetSubmatrix(await SumMatrix(channels[0], channels[1]), 0, 0);
            resultMatrix.SetSubmatrix(await SumMatrix(channels[2], channels[3]), 0, matrixA.Width / 2);
            resultMatrix.SetSubmatrix(await SumMatrix(channels[4], channels[5]), matrixA.Width / 2, 0);
            resultMatrix.SetSubmatrix(await SumMatrix(channels[6], channels[7]), matrixA.Width / 2, matrixA.Width / 2);

            lock (_lockerB)
            {
                Console.WriteLine($"Matrix C Height: {resultMatrix.Height}, Width: {resultMatrix.Width}.");
                Console.WriteLine("Matrix C:");
                Console.WriteLine(resultMatrix.ToString());
            }

            await moduleInfo.Parent.WriteObjectAsync(resultMatrix);
        }

        private static async Task<Matrix> SumMatrix(IChannel one, IChannel two)
        {
            var matrix = await one.ReadObjectAsync<Matrix>();
            matrix.Add(await two.ReadObjectAsync<Matrix>());
            return matrix;
        }
    }
}