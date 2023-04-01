using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication
{
    public class WorkerModule : IWorkerModule
    {
        public string Name => "Worker Matrixes Multiplication Module";

        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var matrixA = await channel.ReadObjectAsync<Matrix>(cancellationToken);
            var matrixB = await channel.ReadObjectAsync<Matrix>(cancellationToken);

            var matrixAB = matrixA.MultiplyBy(matrixB, cancellationToken);

            await channel.WriteObjectAsync(matrixAB, cancellationToken);
        }
    }
}