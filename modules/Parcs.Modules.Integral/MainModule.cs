using Parcs.Net;
using System.Diagnostics;
using System.Text.Json;

namespace Parcs.Modules.Integral
{
    public class MainModule : IModule
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
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            double x = moduleOptions.XStart;
            for (int i = 0; i < moduleOptions.PointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(x);
                await channels[i].WriteDataAsync(x + (moduleOptions.XEnd - moduleOptions.XStart) / moduleOptions.PointsNumber);
                await channels[i].WriteDataAsync(moduleOptions.Precision);
                x += (moduleOptions.XEnd - moduleOptions.XStart) / moduleOptions.PointsNumber;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            double result = 0;
            for (int i = moduleOptions.PointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds, Result = result };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);
        }
    }
}