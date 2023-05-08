using Parcs.Net;

namespace Parcs.Modules.ProofOfWork.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var difficulty = await moduleInfo.Parent.ReadIntAsync();

            var prompt = await moduleInfo.Parent.ReadStringAsync();
            var nonceStart = await moduleInfo.Parent.ReadIntAsync();
            var nonceEnd = await moduleInfo.Parent.ReadIntAsync();

            var leadingZeros = new string(Enumerable.Repeat('0', difficulty).ToArray());

            for (int nonce = nonceStart; nonce <= nonceEnd; ++nonce)
            {
                var hashValue = HashService.GetHashValue($"{prompt}{nonce}");

                if (hashValue.StartsWith(leadingZeros))
                {
                    await moduleInfo.Parent.WriteDataAsync(true);
                    await moduleInfo.Parent.WriteDataAsync(nonce);
                }
            }

            await moduleInfo.Parent.WriteDataAsync(false);
        }
    }
}