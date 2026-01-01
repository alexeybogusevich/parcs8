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

                var channels = new IChannel[options.PointsNumber];
                var points = new IPoint[options.PointsNumber];

                for (int i = 0; i < options.PointsNumber; ++i)
                {
                    points[i] = await moduleInfo.CreatePointAsync();
                    channels[i] = await points[i].CreateChannelAsync();
                }

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
                result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                
                if (options.SaveResults)
                {
                    await SaveResultsAsync(moduleInfo, result, options);
                }

                var jsonContent = JsonSerializer.Serialize(result, JsonSerializerOptions);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent), 
                    options.OutputFile);
                
                moduleInfo.Logger.LogInformation("Паралельний TSP завершено за {ElapsedSeconds:F2} секунд", result.ElapsedSeconds);
                moduleInfo.Logger.LogInformation("Найкраща відстань: {BestDistance:F2}", result.BestDistance);
                
                foreach (var point in points)
                {
                    try
                    {
                        await point.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex, "Помилка видалення точки: {Message}", ex.Message);
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
                        moduleInfo.Logger.LogWarning(ex, "Помилка закриття каналу: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Критична помилка в паралельному TSP модулі: {Message}", ex.Message);
                throw;
            }
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
                    moduleInfo.Logger.LogInformation("Генеруємо випадкові міста...");
                }
            }
            
            // Генеруємо випадкові міста як fallback
            moduleInfo.Logger.LogInformation("Генерація {CitiesNumber} випадкових міст з seed={Seed}", 
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
                    moduleInfo.Logger.LogError(ex, "Помилка читання результату з каналу: {Message}", ex.Message);
                }
            }
            
            if (workerResults.Count == 0)
            {
                throw new InvalidOperationException("Не вдалося отримати результати з жодного worker модуля");
            }
            
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
        
        private static async Task SaveResultsAsync(IModuleInfo moduleInfo, ModuleOutput result, ModuleOptions options)
        {
            try
            {
                var routeContent = $"Найкращий маршрут TSP (відстань: {result.BestDistance:F2})\n";
                routeContent += $"Кількість міст: {result.BestRoute.Count}\n";
                routeContent += $"Поколінь виконано: {result.GenerationsCompleted}\n";
                routeContent += $"Час виконання: {result.ElapsedSeconds:F2} сек\n";
                routeContent += $"Тип виконання: Паралельний\n\n";
                routeContent += "Маршрут: " + string.Join(" → ", result.BestRoute) + " → " + result.BestRoute[0];
                
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(routeContent), 
                    options.BestRouteFile);
                
                moduleInfo.Logger.LogInformation("Найкращий маршрут збережено у файл: {BestRouteFile}", options.BestRouteFile);
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Помилка збереження результатів: {Message}", ex.Message);
            }
        }
    }
} 