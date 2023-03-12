using Parcs.Core;

namespace Parcs.HostAPI.Modules
{
    public class MainModuleSample : IMainModule
    {
        private readonly ILogger<MainModuleSample> _logger;

        public MainModuleSample(ILogger<MainModuleSample> logger)
        {
            _logger = logger;
        }

        public async Task<ModuleOutput> RunAsync(IHostInfo hostInfo, IInputReader inputReader, CancellationToken cancellationToken = default)
        {
            await using var inputFileStream1 = inputReader.MoveNext();
            await using var inputFileStream2 = inputReader.MoveNext();

            using (var reader = new StreamReader(inputFileStream1))
            {
                Console.WriteLine(await reader.ReadToEndAsync(cancellationToken));
            }

            using (var reader = new StreamReader(inputFileStream2))
            {
                Console.WriteLine(await reader.ReadToEndAsync(cancellationToken));
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