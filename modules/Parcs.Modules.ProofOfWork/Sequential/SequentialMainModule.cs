using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.ProofOfWork.Sequential
{
    public class SequentialMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            long? resultNonce = null;
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            for (long nonce = 0; nonce <= moduleOptions.MaximumNonce; ++nonce)
            {
                var hashValue = HashService.GetHashValue($"{moduleOptions.Prompt}{nonce}");

                if (hashValue.StartsWith(new string(Enumerable.Repeat('0', moduleOptions.Difficulty).ToArray())))
                {
                    resultNonce = nonce;
                    break;
                }
            }

            stopWatch.Stop();

            var moduleOutput = new ModuleOutput
            {
                Found = resultNonce is not null,
                ElapsedSeconds = stopWatch.Elapsed.TotalSeconds,
                ResultNonce = resultNonce,
                ResultHash = resultNonce is null ? null : HashService.GetHashValue($"{moduleOptions.Prompt}{resultNonce}"),
            };

            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);
        }
    }
}