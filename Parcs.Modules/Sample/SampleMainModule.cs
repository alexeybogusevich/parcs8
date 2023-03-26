using Parcs.Net;
using System.Text;

namespace Parcs.Modules.Sample
{
    public class SampleMainModule : IMainModule
    {
        public string Name => "Sample main module";

        public async Task RunAsync(IHostInfo hostInfo, IInputReader inputReader, IOutputWriter outputWriter, CancellationToken cancellationToken = default)
        {
            foreach (var filename in inputReader.GetFilenames())
            {
                await using var fileStream = inputReader.GetFileStreamForFile(filename);
                using var streamReader = new StreamReader(fileStream);
                Console.WriteLine(await streamReader.ReadToEndAsync(cancellationToken));
            }

            var pointsNumber = hostInfo.AvailablePointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await channels[i].ExecuteClassAsync("Parcs.Modules", "SampleWorkerModule");
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(10.1D, cancellationToken);
                await channels[i].WriteDataAsync(true, cancellationToken);
                await channels[i].WriteDataAsync("Hello world", cancellationToken);
                await channels[i].WriteDataAsync((byte)1, cancellationToken);
                await channels[i].WriteDataAsync(123L, cancellationToken);
                await channels[i].WriteDataAsync(22, cancellationToken);
            }

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync(cancellationToken);
            }

            await outputWriter.WriteToFileAsync(Encoding.UTF8.GetBytes("Hello world!"), "test.txt",  cancellationToken);

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i].Delete();
            }
        }
    }
}