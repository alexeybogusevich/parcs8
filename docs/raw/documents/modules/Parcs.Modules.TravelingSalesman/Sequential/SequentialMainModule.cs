using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;
using System.IO; // Added for file operations
using System.Text.Json; // Added for JSON serialization

namespace Parcs.Modules.TravelingSalesman.Sequential
{
    public class SequentialMainModule : IModule
    {
        public IModuleInfo GetModuleInfo()
        {
            return new ModuleInfo
            {
                Name = "Sequential TSP Module",
                Description = "Sequential implementation of Traveling Salesman Problem using Genetic Algorithm"
            };
        }

        public void Run(IModuleInfo moduleInfo, IChannel channel)
        {
            var options = channel.ReadObject<ModuleOptions>();
            var cities = LoadOrGenerateCities(options);
            
            Console.WriteLine($"Запуск послідовного TSP модуля з {cities.Count} містами");
            Console.WriteLine($"Параметри: Population={options.PopulationSize}, Generations={options.Generations}");
            
            var stopwatch = Stopwatch.StartNew();
            var result = RunGeneticAlgorithm(cities, options);
            stopwatch.Stop();
            
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            
            if (options.SaveResults)
            {
                SaveResults(result, options);
            }
            
            channel.WriteObject(result);
            Console.WriteLine($"Послідовний TSP завершено за {result.ElapsedSeconds:F2} секунд");
            Console.WriteLine($"Найкраща відстань: {result.BestDistance:F2}");
        }
        
        private List<City> LoadOrGenerateCities(ModuleOptions options)
        {
            if (options.LoadFromFile && !string.IsNullOrEmpty(options.InputFile))
            {
                try
                {
                    Console.WriteLine($"Завантаження міст з файлу: {options.InputFile}");
                    
                    if (options.InputFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        return CityLoader.LoadFromJsonFile(options.InputFile);
                    }
                    else
                    {
                        return CityLoader.LoadFromTextFile(options.InputFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка завантаження з файлу: {ex.Message}");
                    Console.WriteLine("Генеруємо випадкові міста...");
                }
            }
            
            // Генеруємо випадкові міста як fallback
            Console.WriteLine($"Генерація {options.CitiesNumber} випадкових міст з seed={options.Seed}");
            return CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
        }
        
        private ModuleOutput RunGeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            var ga = new GeneticAlgorithm(cities, options);
            ga.Initialize();
            
            Console.WriteLine($"Початкова популяція: {options.PopulationSize} особин");
            Console.WriteLine($"Початкова найкраща відстань: {ga.GetBestRoute().TotalDistance:F2}");
            
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
        
        private void SaveResults(ModuleOutput result, ModuleOptions options)
        {
            try
            {
                // Зберігаємо результати у JSON
                var jsonContent = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(options.OutputFile, jsonContent);
                Console.WriteLine($"Результати збережено у файл: {options.OutputFile}");
                
                // Зберігаємо найкращий маршрут у текстовому форматі
                var routeContent = $"Найкращий маршрут TSP (відстань: {result.BestDistance:F2})\n";
                routeContent += $"Кількість міст: {result.BestRoute.Count}\n";
                routeContent += $"Поколінь виконано: {result.GenerationsCompleted}\n";
                routeContent += $"Час виконання: {result.ElapsedSeconds:F2} сек\n\n";
                routeContent += "Маршрут: " + string.Join(" → ", result.BestRoute) + " → " + result.BestRoute[0];
                
                File.WriteAllText(options.BestRouteFile, routeContent);
                Console.WriteLine($"Найкращий маршрут збережено у файл: {options.BestRouteFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка збереження результатів: {ex.Message}");
            }
        }
    }
} 