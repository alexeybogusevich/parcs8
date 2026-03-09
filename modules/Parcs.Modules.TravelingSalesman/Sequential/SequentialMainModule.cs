using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Sequential
{
    public class SequentialMainModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.BindModuleOptions<ModuleOptions>();
            var cities = await LoadOrGenerateCitiesAsync(moduleInfo, options);
            
            moduleInfo.Logger.LogInformation("Starting sequential TSP module with {CitiesCount} cities", cities.Count);
            moduleInfo.Logger.LogInformation("Parameters: Population={Population}, Generations={Generations}",
                options.PopulationSize, options.Generations);
            
            var stopwatch = Stopwatch.StartNew();
            var result = RunGeneticAlgorithm(moduleInfo, cities, options);
            stopwatch.Stop();
            
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            
            if (options.SaveResults)
            {
                await SaveResultsAsync(moduleInfo, cities, result, options);
            }
            
            // Save result to file
            var jsonContent = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await moduleInfo.OutputWriter.WriteToFileAsync(
                System.Text.Encoding.UTF8.GetBytes(jsonContent), 
                options.OutputFile);
            
            moduleInfo.Logger.LogInformation("Sequential TSP completed in {ElapsedSeconds:F2} seconds", result.ElapsedSeconds);
            moduleInfo.Logger.LogInformation("Best distance: {BestDistance:F2}", result.BestDistance);
        }
        
        private async Task<List<City>> LoadOrGenerateCitiesAsync(IModuleInfo moduleInfo, ModuleOptions options)
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
        
        private ModuleOutput RunGeneticAlgorithm(IModuleInfo moduleInfo, List<City> cities, ModuleOptions options)
        {
            var ga = new GeneticAlgorithm(cities, options);
            ga.Initialize();
            
            moduleInfo.Logger.LogInformation("Initial population: {PopulationSize} individuals", options.PopulationSize);
            moduleInfo.Logger.LogInformation("Initial best distance: {BestDistance:F2}", ga.GetBestRoute().TotalDistance);

            // Log progress every 10% of generations so the job isn't a silent black box.
            int progressInterval = Math.Max(1, options.Generations / 10);
            ga.RunGenerations(options.Generations, (gen, best) =>
            {
                if (gen % progressInterval == 0)
                    moduleInfo.Logger.LogInformation(
                        "Progress: generation {Gen}/{Total} — best distance: {Best:F2}",
                        gen, options.Generations, best);
            });
            
            var bestRoute = ga.GetBestRoute();
            var convergenceHistory = ga.GetConvergenceHistory();
            
            return new ModuleOutput
            {
                BestDistance = bestRoute.TotalDistance,
                AverageDistance = ga.GetAverageDistance(),
                GenerationsCompleted = convergenceHistory.Count,
                ConvergenceHistory = convergenceHistory,
                BestRoute = bestRoute.Cities
            };
        }
        
        private static async Task SaveResultsAsync(
            IModuleInfo  moduleInfo,
            List<City>   cities,
            ModuleOutput result,
            ModuleOptions options)
        {
            try
            {
                // --- best_route.txt ---
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== TSP Best Route — Sequential ===");
                sb.AppendLine();
                sb.AppendLine($"Best distance    : {result.BestDistance:F2}");
                sb.AppendLine($"Average distance : {result.AverageDistance:F2}");
                sb.AppendLine($"Cities           : {result.BestRoute.Count}");
                sb.AppendLine($"Generations      : {result.GenerationsCompleted}");
                sb.AppendLine();
                sb.AppendLine("--- Timing breakdown ---");
                sb.AppendLine($"Total elapsed    : {result.ElapsedSeconds:F2} s");
                sb.AppendLine();
                sb.AppendLine("--- Best route ---");
                sb.AppendLine(string.Join(" → ", result.BestRoute) + " → " + result.BestRoute[0]);

                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                    options.BestRouteFile);

                // --- best_route.svg (tour visualisation) ---
                var routeSvg = SvgGenerator.GenerateRouteSvg(cities, result.BestRoute);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(routeSvg),
                    "best_route.svg");

                // --- convergence.svg (line chart) ---
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