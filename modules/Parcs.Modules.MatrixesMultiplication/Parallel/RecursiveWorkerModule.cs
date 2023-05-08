using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Parallel
{
    public class RecursiveWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            var pointsNumber = moduleInfo.ArgumentsProvider.GetPointsNumber();
            var points = new IPoint[pointsNumber];
            var channels = new IChannel[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();

                if (matrixA.Width / pointsNumber > 1 && pointsNumber > 1)
                {
                    await points[i].ExecuteClassAsync<RecursiveWorkerModule>();
                }
                else
                {
                    await points[i].ExecuteClassAsync<AtomicWorkerModule>();
                }
            }

            var matrixABPairs = pointsNumber switch
            {
                1 => new Tuple<Matrix, Matrix>[] { new(matrixA, matrixB) },
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

            var matrixC = pointsNumber switch
            {
                1 => matrixCPairs.First(),
                2 => MatrixDivisioner.Join2(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                4 => MatrixDivisioner.Join4(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                8 => MatrixDivisioner.Join8(new Matrix(matrixA.Height, matrixB.Width), matrixCPairs),
                _ => throw new NotSupportedException(),
            };

            await moduleInfo.Parent.WriteObjectAsync(matrixC);
        }
    }
}