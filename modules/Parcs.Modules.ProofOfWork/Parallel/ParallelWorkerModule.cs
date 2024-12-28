using Parcs.Net;

namespace Parcs.Modules.ProofOfWork.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"WORKER: Started at {DateTime.UtcNow}");

            var difficulty = await moduleInfo.Parent.ReadIntAsync();

            Console.WriteLine($"WORKER: Received difficulty at {DateTime.UtcNow}");

            var prompt = await moduleInfo.Parent.ReadStringAsync();
            var nonceStart = await moduleInfo.Parent.ReadLongAsync();
            var nonceEnd = await moduleInfo.Parent.ReadLongAsync();

            Console.WriteLine($"WORKER: Received all data at {DateTime.UtcNow}");

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

            Console.WriteLine($"WORKER: Finished at {DateTime.UtcNow}");
        }
    }
}