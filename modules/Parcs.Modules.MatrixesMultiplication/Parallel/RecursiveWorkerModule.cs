using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Parallel
{
    public class RecursiveWorkerModule : IModule
    {
        private readonly object _lockerA = new ();
        private readonly object _lockerB = new ();

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

            for (int i = 0; i < 8; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();

                if (matrixA.Width > 4)
                {
                    await points[i].ExecuteClassAsync<RecursiveWorkerModule>();
                }
                else
                {
                    await points[i].ExecuteClassAsync<AtomicWorkerModule>();
                }
            }

            var matrixABPairs = MatrixDivisioner.Divide8(matrixA, matrixB).ToArray();

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

            var matrixC = MatrixDivisioner.Join8(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs);

            lock (_lockerB)
            {
                Console.WriteLine($"Matrix C Height: {matrixA.Height}, Width: {matrixA.Width}.");
                Console.WriteLine("Matrix C:");
                Console.WriteLine(matrixC.ToString());
            }

            await moduleInfo.Parent.WriteObjectAsync(matrixC);
        }
    }
}