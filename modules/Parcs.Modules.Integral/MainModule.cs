using Parcs.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Parcs.Modules.Integral
{
    public class MainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            double a = 0;
            double b = Math.PI / 2;
            double h = moduleOptions.Precision;

            var pointsNumber = moduleInfo.ArgumentsProvider.GetPointsNumber();
            var points = new IPoint[pointsNumber];
            var channels = new IChannel[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            double y = a;
            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(y);
                await channels[i].WriteDataAsync(y + (b - a) / pointsNumber);
                await channels[i].WriteDataAsync(h);
                y += (b - a) / pointsNumber;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            stopwatch.Stop();

            var moduleOutput = new ModuleOutput { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds, Result = result };
            await moduleInfo.OutputWriter.WriteToFileAsync(JsonSerializer.SerializeToUtf8Bytes(moduleOutput), moduleOptions.OutputFilename);
        }
    }
}