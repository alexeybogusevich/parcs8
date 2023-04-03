using Parcs.Modules.Sample.Models;
using Parcs.Net;
using System.Text;

namespace Parcs.Modules.Sample
{
    public class MainModule : IMainModule
    {
        public string Name => "Sample main module";

        public async Task RunAsync(IArgumentsProvider argumentsProvider, IHostInfo hostInfo, CancellationToken cancellationToken = default)
        {
            if (argumentsProvider.TryGet("sample-argument", out var sampleArgument))
            {
                Console.WriteLine(sampleArgument);
            }

            var inputReader = hostInfo.GetInputReader();

            foreach (var filename in inputReader.GetFilenames())
            {
                await using var fileStream = inputReader.GetFileStreamForFile(filename);
                using var streamReader = new StreamReader(fileStream);
                Console.WriteLine(await streamReader.ReadToEndAsync(cancellationToken));
            }

            var pointsNumber = hostInfo.CanCreatePointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(10.1D);
                await channels[i].WriteDataAsync(true);
                await channels[i].WriteDataAsync("Hello world");
                await channels[i].WriteDataAsync((byte)1);
                await channels[i].WriteDataAsync(123L);
                await channels[i].WriteDataAsync(22);
                await channels[i].WriteObjectAsync(new SampleClass { Id = Guid.NewGuid(), Name = "Test" });
            }

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            var outputWriter = hostInfo.GetOutputWriter();
            await outputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes("Hello world!"), "test.txt");

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }
        }
    }
}