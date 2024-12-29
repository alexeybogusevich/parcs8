using Microsoft.Extensions.Logging;
using Parcs.Net;

namespace Parcs.Modules.ProofOfWork.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("WORKER: Started at {Time}", DateTime.UtcNow);

            var difficulty = await moduleInfo.Parent.ReadIntAsync();

            moduleInfo.Logger.LogInformation("WORKER: Received difficulty at {Time}", DateTime.UtcNow);

            var prompt = await moduleInfo.Parent.ReadStringAsync();
            var nonceStart = await moduleInfo.Parent.ReadLongAsync();
            var nonceEnd = await moduleInfo.Parent.ReadLongAsync();

            moduleInfo.Logger.LogInformation("WORKER: Received all data at {Time}", DateTime.UtcNow);

            var leadingZeros = new string(Enumerable.Repeat('0', difficulty).ToArray());

            for (long nonce = nonceStart; nonce <= nonceEnd; ++nonce)
            {
                var hashValue = HashService.GetHashValue($"{prompt}{nonce}");

                if (hashValue.StartsWith(leadingZeros))
                {
                    await moduleInfo.Parent.WriteDataAsync(true);
                    await moduleInfo.Parent.WriteDataAsync(nonce);
                }
            }

            await moduleInfo.Parent.WriteDataAsync(false);

            moduleInfo.Logger.LogInformation("WORKER: Finished at {Time}", DateTime.UtcNow);
        }
    }
}