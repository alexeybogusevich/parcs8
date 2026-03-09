using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    public class ParallelMainModule : IModule
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = moduleInfo.BindModuleOptions<ModuleOptions>();
                var cities = await LoadOrGenerateCitiesAsync(moduleInfo, options);
                
                options.CitiesNumber = cities.Count;
                
                var stopwatch = Stopwatch.StartNew();

                // ── Phase 1: Pod provisioning ──────────────────────────────────────────
                // Batch-create all points: all Service Bus messages are published before any
                // TCP connection is awaited, so KEDA sees the full queue depth at once.
                var points   = await moduleInfo.CreatePointsAsync(options.PointsNumber);
                var channels = new IChannel[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    channels[i] = await points[i].CreateChannelAsync();
                }
                // All daemons are connected — record infrastructure overhead.
                var podProvisioningSeconds = stopwatch.Elapsed.TotalSeconds;

                // ── Phase 2: Computation ───────────────────────────────────────────────
                foreach (var point in points)
                {
                    await point.ExecuteClassAsync<ParallelWorkerModule>();
                }

                for (int i = 0; i < points.Length; ++i)
                {
                    await channels[i].WriteObjectAsync(cities);
                }

                var result = await CollectResultsAsync(moduleInfo, channels, cities, options);

                stopwatch.Stop();
                result.ElapsedSeconds         = stopwatch.Elapsed.TotalSeconds;
                result.PodProvisioningSeconds = podProvisioningSeconds;
                result.ComputeSeconds         = result.ElapsedSeconds - podProvisioningSeconds;

                if (options.SaveResults)
                {
                    await SaveResultsAsync(moduleInfo, cities, result, options);
                }

                var jsonContent = JsonSerializer.Serialize(result, JsonSerializerOptions);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent), 
                    options.OutputFile);
                
                moduleInfo.Logger.LogInformation("Parallel TSP completed in {ElapsedSeconds:F2} seconds", result.ElapsedSeconds);
                moduleInfo.Logger.LogInformation("Best distance: {BestDistance:F2}", result.BestDistance);
                
                foreach (var point in points)
                {
                    try
                    {
                        await point.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex, "Error deleting point: {Message}", ex.Message);
                    }
                }
                
                foreach (var channel in channels)
                {
                    try
                    {
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex, "Error closing channel: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Critical error in parallel TSP module: {Message}", ex.Message);
                throw;
            }
        }
        
        private static async Task<List<City>> LoadOrGenerateCitiesAsync(IModuleInfo moduleInfo, ModuleOptions options)
        {
            if (options.LoadFromFile && !string.IsNullOrEmpty(options.InputFile))
            {
                try
                {
                    moduleInfo.Logger.LogInformation("Loading cities from file: {InputFile}", options.InputFile);
                    
                    if (options.InputFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(options.InputFile);
                        var jsonContent = await new StreamReader(fileStream).ReadToEndAsync();
                        var cities = JsonSerializer.Deserialize<List<City>>(jsonContent);
                        if (cities != null && cities.Count > 0)
                            return cities;
                    }
                    else
                    {
                        using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(options.InputFile);
                        return CityLoader.LoadFromTextFile(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    moduleInfo.Logger.LogWarning(ex, "Error loading from file: {Message}", ex.Message);
                    moduleInfo.Logger.LogInformation("Generating random cities...");
                }
            }
            
            // Generate random cities as fallback
            moduleInfo.Logger.LogInformation("Generating {CitiesNumber} random cities with seed={Seed}",
                options.CitiesNumber, options.Seed);
            return CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
        }
        
        private async Task<ModuleOutput> CollectResultsAsync(
            IModuleInfo moduleInfo,
            IChannel[] channels,
            List<City> cities,
            ModuleOptions options)
        {
            var workerResults = new List<ModuleOutput>();
            
            foreach (var channel in channels)
            {
                try
                {
                    var result = await channel.ReadObjectAsync<ModuleOutput>();
                    workerResults.Add(result);
                }
                catch (Exception ex)
                {
                    moduleInfo.Logger.LogError(ex, "Error reading result from channel: {Message}", ex.Message);
                }
            }
            
            if (workerResults.Count == 0)
                throw new InvalidOperationException("Failed to receive results from any worker module");

            if (workerResults.Count < channels.Length)
                moduleInfo.Logger.LogWarning(
                    "Only {Received}/{Expected} workers returned results; proceeding with partial results",
                    workerResults.Count, channels.Length);

            return CombineResults(workerResults, cities, options);
        }
        
        private ModuleOutput CombineResults(List<ModuleOutput> workerResults, List<City> cities, ModuleOptions options)
        {
            var bestResult = workerResults.OrderBy(r => r.BestDistance).First();
            
            var combinedHistory = CombineConvergenceHistories(workerResults);
            
            return new ModuleOutput
            {
                BestDistance = bestResult.BestDistance,
                AverageDistance = workerResults.Average(r => r.AverageDistance),
                GenerationsCompleted = workerResults.Max(r => r.GenerationsCompleted),
                ConvergenceHistory = combinedHistory,
                BestRoute = bestResult.BestRoute
            };
        }
        
        private static List<double> CombineConvergenceHistories(List<ModuleOutput> workerResults)
        {
            var combined = new List<double>();
            var maxGenerations = workerResults.Max(r => r.ConvergenceHistory.Count);
            
            for (int gen = 0; gen < maxGenerations; gen++)
            {
                var generationValues = new List<double>();
                foreach (var result in workerResults)
                {
                    if (gen < result.ConvergenceHistory.Count)
                    {
                        generationValues.Add(result.ConvergenceHistory[gen]);
                    }
                }
                
                if (generationValues.Count > 0)
                {
                    combined.Add(generationValues.Average());
                }
            }
            
            return combined;
        }
        
        private static async Task SaveResultsAsync(
            IModuleInfo   moduleInfo,
            List<City>    cities,
            ModuleOutput  result,
            ModuleOptions options)
        {
            try
            {
                // --- best_route.txt ---
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== TSP Best Route — Parallel (Independent Islands) ===");
                sb.AppendLine();
                sb.AppendLine($"Best distance    : {result.BestDistance:F2}");
                sb.AppendLine($"Average distance : {result.AverageDistance:F2}");
                sb.AppendLine($"Cities           : {result.BestRoute.Count}");
                sb.AppendLine($"Generations      : {result.GenerationsCompleted}");
                sb.AppendLine();
                sb.AppendLine("--- Timing breakdown ---");
                sb.AppendLine($"Pod provisioning : {result.PodProvisioningSeconds:F2} s  (KEDA scheduling + pod startup)");
                sb.AppendLine($"Computation      : {result.ComputeSeconds:F2} s  (actual GA work)");
                sb.AppendLine($"Total elapsed    : {result.ElapsedSeconds:F2} s");
                sb.AppendLine();
                sb.AppendLine("--- Best route ---");
                sb.AppendLine(string.Join(" → ", result.BestRoute) + " → " + result.BestRoute[0]);

                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                    options.BestRouteFile);

                // --- best_route.svg ---
                var routeSvg = SvgGenerator.GenerateRouteSvg(cities, result.BestRoute);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(routeSvg),
                    "best_route.svg");

                // --- convergence.svg ---
                if (result.ConvergenceHistory.Count >= 2)
                {
                    var convergenceSvg = SvgGenerator.GenerateConvergenceSvg(result.ConvergenceHistory);
                    await moduleInfo.OutputWriter.WriteToFileAsync(
                        System.Text.Encoding.UTF8.GetBytes(convergenceSvg),
                        "convergence.svg");
                }

                moduleInfo.Logger.LogInformation(
                    "Results saved: {BestRouteFile}, best_route.svg, convergence.svg",
                    options.BestRouteFile);
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Error saving results: {Message}", ex.Message);
            }
        }
    }
} 