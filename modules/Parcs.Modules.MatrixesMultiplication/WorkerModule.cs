using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication
{
    public class WorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            var matrixAB = matrixA.MultiplyBy(matrixB, cancellationToken);

            await moduleInfo.Parent.WriteObjectAsync(matrixAB);
        }
    }
}