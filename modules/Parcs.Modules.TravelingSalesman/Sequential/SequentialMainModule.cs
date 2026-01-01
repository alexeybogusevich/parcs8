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
            
            moduleInfo.Logger.LogInformation("Запуск послідовного TSP модуля з {CitiesCount} містами", cities.Count);
            moduleInfo.Logger.LogInformation("Параметри: Population={Population}, Generations={Generations}", 
                options.PopulationSize, options.Generations);
            
            var stopwatch = Stopwatch.StartNew();
            var result = RunGeneticAlgorithm(moduleInfo, cities, options);
            stopwatch.Stop();
            
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            
            if (options.SaveResults)
            {
                await SaveResultsAsync(moduleInfo, result, options);
            }
            
            // Зберігаємо результат у файл
            var jsonContent = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await moduleInfo.OutputWriter.WriteToFileAsync(
                System.Text.Encoding.UTF8.GetBytes(jsonContent), 
                options.OutputFile);
            
            moduleInfo.Logger.LogInformation("Послідовний TSP завершено за {ElapsedSeconds:F2} секунд", result.ElapsedSeconds);
            moduleInfo.Logger.LogInformation("Найкраща відстань: {BestDistance:F2}", result.BestDistance);
        }
        
        private async Task<List<City>> LoadOrGenerateCitiesAsync(IModuleInfo moduleInfo, ModuleOptions options)
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
        
        private ModuleOutput RunGeneticAlgorithm(IModuleInfo moduleInfo, List<City> cities, ModuleOptions options)
        {
            var ga = new GeneticAlgorithm(cities, options);
            ga.Initialize();
            
            moduleInfo.Logger.LogInformation("Початкова популяція: {PopulationSize} особин", options.PopulationSize);
            moduleInfo.Logger.LogInformation("Початкова найкраща відстань: {BestDistance:F2}", ga.GetBestRoute().TotalDistance);
            
            ga.RunGenerations(options.Generations);
            
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
        
        private async Task SaveResultsAsync(IModuleInfo moduleInfo, ModuleOutput result, ModuleOptions options)
        {
            try
            {
                // Зберігаємо найкращий маршрут у текстовому форматі
                var routeContent = $"Найкращий маршрут TSP (відстань: {result.BestDistance:F2})\n";
                routeContent += $"Кількість міст: {result.BestRoute.Count}\n";
                routeContent += $"Поколінь виконано: {result.GenerationsCompleted}\n";
                routeContent += $"Час виконання: {result.ElapsedSeconds:F2} сек\n\n";
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