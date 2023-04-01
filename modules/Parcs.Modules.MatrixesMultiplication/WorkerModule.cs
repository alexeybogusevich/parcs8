using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication
{
    public class WorkerModule : IWorkerModule
    {
        public string Name => "Worker Matrixes Multiplication Module";

        public async Task RunAsync(IChannel channel)
        {
            var matrixA = await channel.ReadObjectAsync<Matrix>();
            var matrixB = await channel.ReadObjectAsync<Matrix>();

            var matrixAB = matrixA.MultiplyBy(matrixB);

            await channel.WriteObjectAsync(matrixAB);
        }
    }
}