using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.ProofOfWork.Gpu
{
    /// <summary>
    /// GPU-accelerated main module for blockchain proof-of-work nonce search.
    ///
    /// Orchestration is identical to <c>ParallelMainModule</c> (CPU variant); only the
    /// worker type changes to <see cref="GpuWorkerModule"/> so that each daemon node
    /// uses its GPU to pre-filter nonce candidates before SHA-256 verification.
    /// </summary>
    public class GpuMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.BindModuleOptions<ModuleOptions>();

            var points = new IPoint[moduleOptions.PointsNumber];
            var channels = new IChannel[moduleOptions.PointsNumber];

            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<GpuWorkerModule>();
            }

            var rangeSize = (moduleOptions.NonceEnd - moduleOptions.NonceStart + 1) / moduleOptions.PointsNumber;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(moduleOptions.Difficulty);
                await channels[i].WriteDataAsync(moduleOptions.Prompt);
                await channels[i].WriteDataAsync(moduleOptions.NonceStart + rangeSize * i);
                await channels[i].WriteDataAsync(moduleOptions.NonceStart + rangeSize * (i + 1) - 1);
            }

            long? foundNonce = null;

            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                while (true)
                {
                    var hasResult = await channels[i].ReadDataAsync<bool>();

                    if (hasResult)
                    {
                        var nonce = await channels[i].ReadDataAsync<long>();

                        if (foundNonce is null)
                        {
                            foundNonce = nonce;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput
            {
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                Nonce = foundNonce,
                HashValue = foundNonce.HasValue
                    ? HashService.GetHashValue($"{moduleOptions.Prompt}{foundNonce.Value}")
                    : null
            };

            await moduleInfo.OutputWriter.WriteToFileAsync(
                JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);
        }
    }
}
