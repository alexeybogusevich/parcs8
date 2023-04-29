using Parcs.Net;
using System.Text;

namespace Parcs.Modules.Integral
{
    public class MainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();

            double a = 0;
            double b = Math.PI / 2;
            double h = moduleOptions.Precision ?? 0.00000001;

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
            DateTime time = DateTime.Now;
            Console.WriteLine("Waiting for result...");

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            Console.WriteLine("Result found: res = {0}, time = {1}", result, Math.Round((DateTime.Now - time).TotalSeconds, 3));

            var bytes = Encoding.UTF8.GetBytes(result.ToString());
            await moduleInfo.OutputWriter.WriteToFileAsync(bytes, moduleOptions.OutputFilename);

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }
        }
    }
}