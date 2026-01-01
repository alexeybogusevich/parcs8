using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Net;

namespace Parcs.Modules.MonteCarloPi.Parallel
{
    /// <summary>
    /// Estimates π using Monte Carlo method - perfect for demonstrating distributed speedup!
    /// Each worker generates random points independently, minimal communication needed.
    /// </summary>
    public class MonteCarloMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<MonteCarloOptions>();
            
            moduleInfo.Logger.LogInformation("Monte Carlo π Estimation");
            moduleInfo.Logger.LogInformation("Total samples: {TotalSamples:N0}", options.TotalSamples);
            moduleInfo.Logger.LogInformation("Workers: {Workers}", options.Workers);
            
            var stopwatch = Stopwatch.StartNew();
            
            // Create workers
            var channels = new IChannel[options.Workers];
            var points = new IPoint[options.Workers];
            
            for (int i = 0; i < options.Workers; i++)
            {
                points[i] = await moduleInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<MonteCarloWorkerModule>();
            }
            
            // Distribute work: each worker gets samplesPerWorker samples
            var samplesPerWorker = options.TotalSamples / options.Workers;
            
            moduleInfo.Logger.LogInformation("Distributing {SamplesPerWorker:N0} samples per worker", samplesPerWorker);
            
            // Send work to all workers in parallel
            var tasks = new List<Task<long>>();
            for (int i = 0; i < options.Workers; i++)
            {
                int workerIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await channels[workerIndex].WriteDataAsync(samplesPerWorker);
                    await channels[workerIndex].WriteDataAsync(options.Seed + workerIndex); // Different seed per worker
                    var hits = await channels[workerIndex].ReadDataAsync<long>();
                    return hits;
                }));
            }
            
            // Collect results
            var allHits = await Task.WhenAll(tasks);
            long totalHits = allHits.Sum();
            
            stopwatch.Stop();
            
            // Calculate π: π ≈ 4 × (points inside circle) / (total points)
            double piEstimate = 4.0 * totalHits / options.TotalSamples;
            double error = Math.Abs(piEstimate - Math.PI);
            double errorPercent = (error / Math.PI) * 100;
            
            moduleInfo.Logger.LogInformation("Results:");
            moduleInfo.Logger.LogInformation("  Estimated π: {PiEstimate:F10}", piEstimate);
            moduleInfo.Logger.LogInformation("  Actual π:    {ActualPi:F10}", Math.PI);
            moduleInfo.Logger.LogInformation("  Error:       {Error:F10} ({ErrorPercent:F4}%)", error, errorPercent);
            moduleInfo.Logger.LogInformation("  Time:        {ElapsedSeconds:F2} seconds", stopwatch.Elapsed.TotalSeconds);
            moduleInfo.Logger.LogInformation("  Throughput:  {Throughput:N0} samples/second", options.TotalSamples / stopwatch.Elapsed.TotalSeconds);
            
            // Cleanup
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
    
    public class MonteCarloOptions
    {
        public long TotalSamples { get; set; } = 100_000_000; // 100M samples
        public int Workers { get; set; } = 4;
        public int Seed { get; set; } = 42;
    }
}

