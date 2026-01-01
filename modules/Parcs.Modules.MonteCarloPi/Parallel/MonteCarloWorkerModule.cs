using Microsoft.Extensions.Logging;
using Parcs.Net;

namespace Parcs.Modules.MonteCarloPi.Parallel
{
    /// <summary>
    /// Worker module for Monte Carlo π estimation.
    /// Generates random points and counts how many fall inside unit circle.
    /// </summary>
    public class MonteCarloWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("Monte Carlo Worker started");
            
            // Receive number of samples to process
            var samples = await moduleInfo.Parent.ReadDataAsync<long>();
            var seed = await moduleInfo.Parent.ReadDataAsync<int>();
            
            moduleInfo.Logger.LogInformation("Worker processing {Samples:N0} samples with seed {Seed}", samples, seed);
            
            var random = new Random(seed);
            long hits = 0;
            
            // Generate random points and count hits inside unit circle
            for (long i = 0; i < samples; i++)
            {
                double x = random.NextDouble(); // [0, 1)
                double y = random.NextDouble(); // [0, 1)
                
                // Check if point is inside unit circle (x² + y² ≤ 1)
                if (x * x + y * y <= 1.0)
                {
                    hits++;
                }
            }
            
            moduleInfo.Logger.LogInformation("Worker completed: {Hits:N0} hits out of {Samples:N0} samples", hits, samples);
            
            // Send result back
            await moduleInfo.Parent.WriteDataAsync(hits);
        }
    }
}

