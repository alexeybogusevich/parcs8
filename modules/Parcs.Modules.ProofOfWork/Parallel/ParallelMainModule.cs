using Parcs.Net;
using System.Diagnostics;
using System.Text;

namespace Parcs.Demo1.Parallel
{
    public class ParallelMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
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

            var outputText = $"Elapsed Time: {stopWatch.Elapsed.TotalSeconds} seconds\n";

            if (resultNonce != null)
            {
                outputText += "Result Found: Yes\n";
                outputText += $"Nonce: {resultNonce}\n";
                outputText += $"Hash: {HashService.GetHashValue($"{moduleOptions.Prompt}{resultNonce}")}\n";
            }
            else
            {
                outputText += "Result Found: No\n";
            }

            await moduleInfo.OutputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes(outputText), moduleOptions.OutputFilename);
        }
    }
}
