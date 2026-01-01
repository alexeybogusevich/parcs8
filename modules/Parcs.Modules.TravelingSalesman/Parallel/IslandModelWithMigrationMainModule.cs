using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Island Model with Migration - workers exchange individuals periodically.
    /// Combines benefits of independent exploration (Island Model) with gene exchange (Migration).
    /// </summary>
    public class IslandModelWithMigrationMainModule : IModule
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = moduleInfo.BindModuleOptions<ModuleOptions>();
                var cities = await LoadOrGenerateCitiesAsync(moduleInfo, options);
                
                options.CitiesNumber = cities.Count;
                options.AutoConfigureForLargeProblems();

                moduleInfo.Logger.LogInformation("Запуск Island Model з міграцією для {CitiesCount} міст", cities.Count);
                moduleInfo.Logger.LogInformation("Параметри: Population={Population}, Generations={Generations}, Points={Points}", 
                    options.PopulationSize, options.Generations, options.PointsNumber);
                moduleInfo.Logger.LogInformation("Міграція: Enabled={Enabled}, Type={Type}, Size={Size}, Interval={Interval}", 
                    options.EnableMigration, options.MigrationType, options.MigrationSize, options.MigrationInterval);

                var stopwatch = Stopwatch.StartNew();

                // Create worker points and channels
                var channels = new IChannel[options.PointsNumber];
                var points = new IPoint[options.PointsNumber];

                for (int i = 0; i < options.PointsNumber; i++)
                {
                    points[i] = await moduleInfo.CreatePointAsync();
                    channels[i] = await points[i].CreateChannelAsync();
                }

                moduleInfo.Logger.LogInformation("Створено {PointsCount} островів для паралельної обробки", points.Length);

                // Send cities and options to all workers
                for (int i = 0; i < points.Length; i++)
                {
                    await channels[i].WriteObjectAsync(cities);
                    await channels[i].WriteObjectAsync(options);
                }

                // Launch worker modules
                moduleInfo.Logger.LogInformation("Запуск worker модулів...");
                foreach (var point in points)
                {
                    await point.ExecuteClassAsync<IslandModelWithMigrationWorkerModule>();
                }

                // Workers run independently and signal master when they need migration
                // Master coordinates migration synchronization
                var finalResults = new List<ModuleOutput>();
                
                // Use a simpler approach: workers complete all generations and handle migration internally
                // by communicating with master when needed
                for (int i = 0; i < channels.Length; i++)
                {
                    var result = await channels[i].ReadObjectAsync<ModuleOutput>();
                    finalResults.Add(result);
                }

                stopwatch.Stop();

                // Combine results (pick best from all islands)
                var bestResult = finalResults.OrderBy(r => r.BestDistance).First();
                var combinedResult = new ModuleOutput
                {
                    BestDistance = bestResult.BestDistance,
                    AverageDistance = finalResults.Average(r => r.AverageDistance),
                    GenerationsCompleted = finalResults.Max(r => r.GenerationsCompleted),
                    BestRoute = bestResult.BestRoute,
                    ConvergenceHistory = CombineConvergenceHistories(finalResults),
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
                };

                if (options.SaveResults)
                {
                    await SaveResultsAsync(moduleInfo, combinedResult, options);
                }

                var jsonContent = JsonSerializer.Serialize(combinedResult, JsonSerializerOptions);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent),
                    options.OutputFile);

                moduleInfo.Logger.LogInformation("Island Model з міграцією завершено за {ElapsedSeconds:F2} секунд", combinedResult.ElapsedSeconds);
                moduleInfo.Logger.LogInformation("Найкраща відстань: {BestDistance:F2}", combinedResult.BestDistance);

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
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Критична помилка в Island Model з міграцією: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Performs migration between islands - exchanges individuals between workers
        /// </summary>
        private static async Task PerformMigrationAsync(
            IModuleInfo moduleInfo,
            IChannel[] channels,
            ModuleOptions options)
        {
            // Request migrants from all workers
            for (int i = 0; i < channels.Length; i++)
            {
                await channels[i].WriteSignalAsync(Signal.ExecuteClass); // Signal to send migrants
            }

            // Collect migrants from all workers
            var allMigrants = new List<List<Route>>();
            for (int i = 0; i < channels.Length; i++)
            {
                var migrants = await channels[i].ReadObjectAsync<List<Route>>();
                allMigrants.Add(migrants);
                moduleInfo.Logger.LogInformation("Острів {Island} надіслав {Count} мігрантів", i, migrants.Count);
            }

            // Distribute migrants using ring topology: island i sends to island (i+1) mod N
            for (int i = 0; i < channels.Length; i++)
            {
                int targetIsland = (i + 1) % channels.Length;
                var migrantsToSend = allMigrants[i];
                
                await channels[targetIsland].WriteObjectAsync(migrantsToSend);
                moduleInfo.Logger.LogInformation("Міграція: Острів {From} → Острів {To} ({Count} особин)", 
                    i, targetIsland, migrantsToSend.Count);
            }

            // Signal workers to receive and integrate migrants
            for (int i = 0; i < channels.Length; i++)
            {
                await channels[i].WriteSignalAsync(Signal.ExecuteClass); // Signal to receive migrants
            }

            // Wait for acknowledgment
            for (int i = 0; i < channels.Length; i++)
            {
                await channels[i].ReadBooleanAsync();
            }

            moduleInfo.Logger.LogInformation("Міграція завершена");
        }

        private static List<double> CombineConvergenceHistories(List<ModuleOutput> results)
        {
            if (results.Count == 0) return new List<double>();
            
            int maxLength = results.Max(r => r.ConvergenceHistory?.Count ?? 0);
            var combined = new List<double>();
            
            for (int i = 0; i < maxLength; i++)
            {
                var values = results
                    .Where(r => r.ConvergenceHistory != null && i < r.ConvergenceHistory.Count)
                    .Select(r => r.ConvergenceHistory[i])
                    .ToList();
                
                if (values.Count > 0)
                {
                    combined.Add(values.Min()); // Best across all islands
                }
            }
            
            return combined;
        }

        private static async Task<List<City>> LoadOrGenerateCitiesAsync(IModuleInfo moduleInfo, ModuleOptions options)
        {
            if (options.LoadFromFile && !string.IsNullOrEmpty(options.InputFile))
            {
                try
                {
                    moduleInfo.Logger.LogInformation("Завантаження міст з файлу: {InputFile}", options.InputFile);
                    
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
                    moduleInfo.Logger.LogWarning(ex, "Помилка завантаження з файлу: {Message}", ex.Message);
                }
            }
            
            moduleInfo.Logger.LogInformation("Генерація {CitiesNumber} випадкових міст з seed={Seed}", 
                options.CitiesNumber, options.Seed);
            return CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
        }

        private static async Task SaveResultsAsync(IModuleInfo moduleInfo, ModuleOutput result, ModuleOptions options)
        {
            // Implementation if needed
            await Task.CompletedTask;
        }

        private class WorkerState
        {
            public int WorkerIndex { get; set; }
            public int Generation { get; set; }
            public double BestDistance { get; set; }
            public double AverageDistance { get; set; }
        }

        private class GenerationResult
        {
            public int Generation { get; set; }
            public double BestDistance { get; set; }
            public double AverageDistance { get; set; }
        }
    }
}

