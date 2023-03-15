using Microsoft.Extensions.Logging;
using Parcs.Core;

namespace Parcs.Modules.Sample
{
    public class SampleMainModule : IMainModule
    {
        private readonly ILogger<SampleMainModule> _logger;

        public SampleMainModule(ILogger<SampleMainModule> logger)
        {
            _logger = logger;
        }

        public async Task<ModuleOutput> RunAsync(IHostInfo hostInfo, IInputReader inputReader, CancellationToken cancellationToken = default)
        {
            foreach (var filename in inputReader.GetFilenames())
            {
                await using var fileStream = inputReader.GetFileStreamForFile(filename);
                using var streamReader = new StreamReader(fileStream);
                Console.WriteLine(await streamReader.ReadToEndAsync(cancellationToken));
            }

            var pointsNumber = hostInfo.MaximumPointsNumber;
            var channels = new IChannel[pointsNumber];
            var points = new IPoint[pointsNumber];

            _logger.LogInformation("Creating the control space...");

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = points[i].CreateChannel();
                await channels[i].ExecuteClassAsync("Some funny class :)");
            }

            _logger.LogInformation("Sending data...");

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

            _logger.LogInformation("Waiting for result...");

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync(cancellationToken);
            }

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i].Delete();
            }

            return new ModuleOutput
            {
                Result = result,
            };
        }
    }
}