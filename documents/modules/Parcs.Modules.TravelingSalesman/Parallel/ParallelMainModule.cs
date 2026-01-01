using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    public class ParallelMainModule : IModule
    {
        public IModuleInfo GetModuleInfo()
        {
            return new ModuleInfo
            {
                Name = "Parallel TSP Module",
                Description = "Parallel implementation of Traveling Salesman Problem using Genetic Algorithm"
            };
        }

        public void Run(IModuleInfo moduleInfo, IChannel channel)
        {
            var options = channel.ReadObject<ModuleOptions>();
            var cities = LoadOrGenerateCities(options);
            
            Console.WriteLine($"Запуск паралельного TSP модуля з {cities.Count} містами");
            Console.WriteLine($"Параметри: Population={options.PopulationSize}, Generations={options.Generations}, Points={options.PointsNumber}");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Створюємо точки для паралельної обробки
            var points = CreatePoints(options.PointsNumber);
            Console.WriteLine($"Створено {points.Count} точок для паралельної обробки");
            
            // Розподіляємо дані між точками
            DistributeData(points, cities, options);
            
            // Виконуємо робочі модулі
            ExecuteWorkerModules(points);
            
            // Збираємо результати
            var result = CollectResults(points, cities, options);
            
            stopwatch.Stop();
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            
            if (options.SaveResults)
            {
                SaveResults(result, options);
            }
            
            channel.WriteObject(result);
            Console.WriteLine($"Паралельний TSP завершено за {result.ElapsedSeconds:F2} секунд");
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
        
        private List<IPoint> CreatePoints(int count)
        {
            var points = new List<IPoint>();
            for (int i = 0; i < count; i++)
            {
                var point = moduleInfo.CreatePoint();
                points.Add(point);
            }
            return points;
        }
        
        private void DistributeData(List<IPoint> points, List<City> cities, ModuleOptions options)
        {
            foreach (var point in points)
            {
                var channel = moduleInfo.CreateChannel();
                channel.WriteObject(cities);
                channel.WriteObject(options);
                point.ExecuteClassAsync<ParallelWorkerModule>(channel);
            }
        }
        
        private void ExecuteWorkerModules(List<IPoint> points)
        {
            var tasks = points.Select(point => point.ExecuteClassAsync<ParallelWorkerModule>()).ToArray();
            Task.WaitAll(tasks);
        }
        
        private ModuleOutput CollectResults(List<IPoint> points, List<City> cities, ModuleOptions options)
        {
            var workerResults = new List<ModuleOutput>();
            
            foreach (var point in points)
            {
                var channel = moduleInfo.CreateChannel();
                var result = channel.ReadObject<ModuleOutput>();
                workerResults.Add(result);
            }
            
            return CombineResults(workerResults, cities, options);
        }
        
        private ModuleOutput CombineResults(List<ModuleOutput> workerResults, List<City> cities, ModuleOptions options)
        {
            // Знаходимо найкращий результат серед всіх робочих модулів
            var bestResult = workerResults.OrderBy(r => r.BestDistance).First();
            
            // Об'єднуємо історію збіжності
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
        
        private List<double> CombineConvergenceHistories(List<ModuleOutput> workerResults)
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
        
        private void SaveResults(ModuleOutput result, ModuleOptions options)
        {
            try
            {
                // Зберігаємо результати у JSON
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(options.OutputFile, jsonContent);
                Console.WriteLine($"Результати збережено у файл: {options.OutputFile}");
                
                // Зберігаємо найкращий маршрут у текстовому форматі
                var routeContent = $"Найкращий маршрут TSP (відстань: {result.BestDistance:F2})\n";
                routeContent += $"Кількість міст: {result.BestRoute.Count}\n";
                routeContent += $"Поколінь виконано: {result.GenerationsCompleted}\n";
                routeContent += $"Час виконання: {result.ElapsedSeconds:F2} сек\n";
                routeContent += $"Тип виконання: Паралельний\n\n";
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