using Parcs.Modules.MatrixesMultiplication.Models;
using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication.Parallel
{
    public class RecursiveAtomicWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var matrixA = await moduleInfo.Parent.ReadObjectAsync<Matrix>();
            var matrixB = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            matrixA.MultiplyBy(matrixB, cancellationToken);

            await moduleInfo.Parent.WriteObjectAsync(matrixA);
        }
    }
}