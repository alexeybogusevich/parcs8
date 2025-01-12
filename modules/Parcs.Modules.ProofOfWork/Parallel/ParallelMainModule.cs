using Microsoft.Extensions.Logging;
using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.ProofOfWork.Parallel
{
    public class ParallelMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("PARALLEL: Started at {Time}", DateTime.UtcNow);

            var moduleOptions = moduleInfo.BindModuleOptions<ModuleOptions>();

            var channels = new IChannel[moduleOptions.PointsNumber];
            var points = new IPoint[moduleOptions.PointsNumber];

            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
            }

            long? resultNonce = null;
            long nonceStart = 0;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (resultNonce is null && nonceStart < moduleOptions.MaximumNonce)
            {
                for (int i = 0; i < moduleOptions.PointsNumber; ++i)
                {
                    await points[i].ExecuteClassAsync<ParallelWorkerModule>();
                    await channels[i].WriteDataAsync(moduleOptions.Difficulty);
                    await channels[i].WriteDataAsync(moduleOptions.Prompt);
                    await channels[i].WriteDataAsync(nonceStart + moduleOptions.NonceBatchSize * i);
                    await channels[i].WriteDataAsync(nonceStart + moduleOptions.NonceBatchSize * i + moduleOptions.NonceBatchSize);
                }
                
                Console.WriteLine($"PARALLEL: Sent at {DateTime.UtcNow}. Nonce start: {nonceStart}");

                nonceStart += moduleOptions.NonceBatchSize * moduleOptions.PointsNumber;

                for (int i = 0; i < moduleOptions.PointsNumber; ++i)
                {
                    if (await channels[i].ReadBooleanAsync())
                    {
                        resultNonce = await channels[i].ReadLongAsync();
                        break;
                    }
                }
            }

            stopWatch.Stop();

            var moduleOutput = new ModuleOutput
            {
                ElapsedSeconds = stopWatch.Elapsed.TotalSeconds,
            };

            if (resultNonce != null)
            {
                moduleOutput.Found = true;
                moduleOutput.ResultNonce = resultNonce;
                moduleOutput.ResultHash = HashService.GetHashValue($"{moduleOptions.Prompt}{resultNonce}");
            }

            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);

            moduleInfo.Logger.LogInformation("PARALLEL: Finished at {Time}", DateTime.UtcNow);
        }
    }
}