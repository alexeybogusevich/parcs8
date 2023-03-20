using Parcs.Core;
using System.Text;

namespace Parcs.Modules.Sample
{
    public class SampleMainModule : IMainModule
    {
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
                channels[i] = points[i].CreateChannel();
                await channels[i].ExecuteClassAsync("Some funny assembly", "Some funny class :)");
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(10.1D, cancellationToken);
                await channels[i].WriteDataAsync(true, cancellationToken);
                await channels[i].WriteDataAsync("Hello world", cancellationToken);
                await channels[i].WriteDataAsync((byte)1, cancellationToken);
                await channels[i].WriteDataAsync(123L, cancellationToken);
                await channels[i].WriteDataAsync(22, cancellationToken);

                var job = new
                {
                    StartDateUtc = DateTime.UtcNow,
                    Status = JobStatus.InProgress,
                    CreateDateUtc = DateTime.UtcNow.AddDays(-1),
                    EndDateUtc = DateTime.UtcNow.AddDays(1),
                    Id = Guid.NewGuid(),
                };

                await channels[i].WriteObjectAsync(job, cancellationToken);
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