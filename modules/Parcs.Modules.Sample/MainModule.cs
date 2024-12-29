using Microsoft.Extensions.Logging;
using Parcs.Modules.Sample.Models;
using Parcs.Net;
using System.Text;

namespace Parcs.Modules.Sample
{
    public class MainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            foreach (var filename in moduleInfo.InputReader.GetFilenames())
            {
                await using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(filename);
                using var streamReader = new StreamReader(fileStream);
                Console.WriteLine(await streamReader.ReadToEndAsync(cancellationToken));
            }

            var pointsNumber = moduleInfo.BindModuleOptions<ModuleOptions>().PointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(10.1D);
                await channels[i].WriteDataAsync(true);
                await channels[i].WriteDataAsync("Hello world");
                await channels[i].WriteDataAsync((byte)1);
                await channels[i].WriteDataAsync([1, 0, 1]);
                await channels[i].WriteDataAsync(123L);
                await channels[i].WriteDataAsync(22);
                await channels[i].WriteObjectAsync(new SampleClass { Id = Guid.NewGuid(), Name = "Test" });
                await channels[i].WriteDataAsync(true);
            }

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            await moduleInfo.OutputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes("Hello world!"), "test.txt");

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }

            moduleInfo.Logger.LogInformation("This is a sample logging statement.");
        }
    }
}