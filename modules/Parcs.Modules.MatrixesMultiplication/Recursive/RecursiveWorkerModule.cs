using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Recursive
{
    public class RecursiveWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<RecursiveModuleOptions>();

            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            var points = new IPoint[8];
            var channels = new IChannel[8];

            for (int i = 0; i < 8; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();

                if (matrixA.Width / 2 >= moduleOptions.MinimumMatrixSize)
                {
                    await points[i].ExecuteClassAsync<RecursiveWorkerModule>();
                }
                else
                {
                    await points[i].ExecuteClassAsync<RecursiveAtomicWorkerModule>();
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

            await moduleInfo.Parent.WriteObjectAsync(matrixC);
        }
    }
}