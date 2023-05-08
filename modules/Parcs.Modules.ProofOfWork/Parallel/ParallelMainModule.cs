using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.ProofOfWork.Parallel
{
    public class ParallelMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            var pointsNumber = moduleInfo.ArgumentsProvider.GetPointsNumber();
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
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
                for (int i = 0; i < pointsNumber; ++i)
                {
                    await points[i].ExecuteClassAsync<ParallelWorkerModule>();
                    await channels[i].WriteDataAsync(moduleOptions.Difficulty);
                    await channels[i].WriteDataAsync(moduleOptions.Prompt);
                    await channels[i].WriteDataAsync(nonceStart + moduleOptions.NonceBatchSize * i);
                    await channels[i].WriteDataAsync(nonceStart + moduleOptions.NonceBatchSize * i + moduleOptions.NonceBatchSize);
                }

                nonceStart += moduleOptions.NonceBatchSize * pointsNumber;

                for (int i = 0; i < pointsNumber; ++i)
                {
                    var found = await channels[i].ReadBooleanAsync();

                    if (found)
                    {
                        resultNonce = await channels[i].ReadLongAsync();
                        break;
                    }
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