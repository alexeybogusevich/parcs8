using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Net;

namespace Parcs.Modules.MonteCarloPi.Gpu
{
    /// <summary>
    /// GPU-accelerated main module for Monte Carlo π estimation.
    ///
    /// Orchestration is identical to <c>MonteCarloMainModule</c> (CPU variant); only the
    /// worker type changes to <see cref="MonteCarloGpuWorkerModule"/> so that each daemon
    /// node runs millions of samples in parallel on its GPU.
    /// </summary>
    public class MonteCarloGpuMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<MonteCarloOptions>();

            moduleInfo.Logger.LogInformation("Monte Carlo GPU π Estimation");
            moduleInfo.Logger.LogInformation("Total samples: {TotalSamples:N0}", options.TotalSamples);
            moduleInfo.Logger.LogInformation("Workers: {Workers}", options.Workers);

            var stopwatch = Stopwatch.StartNew();

            var channels = new IChannel[options.Workers];
            var points = new IPoint[options.Workers];

            for (int i = 0; i < options.Workers; i++)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<MonteCarloGpuWorkerModule>();
            }

            var samplesPerWorker = options.TotalSamples / options.Workers;

            moduleInfo.Logger.LogInformation(
                "Distributing {SamplesPerWorker:N0} samples per GPU worker", samplesPerWorker);

            var tasks = new List<Task<long>>();
            for (int i = 0; i < options.Workers; i++)
            {
                int workerIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await channels[workerIndex].WriteDataAsync(samplesPerWorker);
                    await channels[workerIndex].WriteDataAsync(options.Seed + workerIndex);
                    return await channels[workerIndex].ReadDataAsync<long>();
                }));
            }

            var allHits = await Task.WhenAll(tasks);
            long totalHits = allHits.Sum();

            stopwatch.Stop();

            double piEstimate = 4.0 * totalHits / options.TotalSamples;
            double error = Math.Abs(piEstimate - Math.PI);
            double errorPercent = (error / Math.PI) * 100;

            moduleInfo.Logger.LogInformation("Results:");
            moduleInfo.Logger.LogInformation("  Estimated π: {PiEstimate:F10}", piEstimate);
            moduleInfo.Logger.LogInformation("  Actual π:    {ActualPi:F10}", Math.PI);
            moduleInfo.Logger.LogInformation("  Error:       {Error:F10} ({ErrorPercent:F4}%)", error, errorPercent);
            moduleInfo.Logger.LogInformation("  Time:        {ElapsedSeconds:F2} seconds", stopwatch.Elapsed.TotalSeconds);
            moduleInfo.Logger.LogInformation("  Throughput:  {Throughput:N0} samples/second",
                options.TotalSamples / stopwatch.Elapsed.TotalSeconds);

            foreach (var point in points)
            {
                try { await point.DeleteAsync(); } catch { }
            }

            foreach (var channel in channels)
            {
                try { channel.Dispose(); } catch { }
            }
        }
    }
}
