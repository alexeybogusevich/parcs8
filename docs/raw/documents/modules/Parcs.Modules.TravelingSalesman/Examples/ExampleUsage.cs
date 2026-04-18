using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Examples
{
    public class ExampleUsage
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Приклад використання TSP модуля ===\n");
            
            // Тестуємо різні конфігурації
            TestDeterministicComparison();
            
            Console.WriteLine("\n=== Тестування завантаження з файлу ===\n");
            TestFileLoading();
            
            Console.WriteLine("\n=== Тестування різних патернів міст ===\n");
            TestCityPatterns();
        }
        
        private static void TestDeterministicComparison()
        {
            Console.WriteLine("1. Порівняння детерміністичних результатів");
            Console.WriteLine("==========================================");
            
            var options = new ModuleOptions
            {
                CitiesNumber = 25,
                PopulationSize = 500,
                Generations = 50,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            // Тест 1: Мала задача з фіксованим seed
            Console.WriteLine($"\nТест 1: {options.CitiesNumber} міст, seed={options.Seed}");
            var cities1 = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
            var result1 = RunGeneticAlgorithm(cities1, options);
            Console.WriteLine($"Результат 1: {result1.BestDistance:F2}");
            
            // Тест 2: Та сама задача з тим самим seed
            var cities2 = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
            var result2 = RunGeneticAlgorithm(cities2, options);
            Console.WriteLine($"Результат 2: {result2.BestDistance:F2}");
            
            if (Math.Abs(result1.BestDistance - result2.BestDistance) < 0.01)
            {
                Console.WriteLine("✓ Результати ідентичні (детермінізм забезпечено)");
            }
            else
            {
                Console.WriteLine("✗ Результати різні (проблема з детермінізмом)");
            }
            
            // Тест 3: Порівняння з різними seed
            Console.WriteLine($"\nТест 3: Порівняння з різними seed");
            var seeds = new[] { 42, 123, 456, 789, 999 };
            var results = new List<double>();
            
            foreach (var seed in seeds)
            {
                var testCities = CityLoader.GenerateTestCities(options.CitiesNumber, seed, TestCityPattern.Random);
                var testResult = RunGeneticAlgorithm(testCities, options);
                results.Add(testResult.BestDistance);
                Console.WriteLine($"Seed {seed}: {testResult.BestDistance:F2}");
            }
            
            var avgResult = results.Average();
            var stdDev = Math.Sqrt(results.Select(r => Math.Pow(r - avgResult, 2)).Average());
            Console.WriteLine($"Середнє: {avgResult:F2}, Стандартне відхилення: {stdDev:F2}");
        }
        
        private static void TestFileLoading()
        {
            Console.WriteLine("2. Тестування завантаження з файлу");
            Console.WriteLine("===================================");
            
            var options = new ModuleOptions
            {
                PopulationSize = 300,
                Generations = 30,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            // Тест завантаження з текстового файлу
            try
            {
                Console.WriteLine("\nТест завантаження з small_grid_16.txt:");
                var citiesFromTxt = CityLoader.LoadFromTextFile("Examples/TestData/small_grid_16.txt");
                Console.WriteLine($"Завантажено {citiesFromTxt.Count} міст");
                
                var resultTxt = RunGeneticAlgorithm(citiesFromTxt, options);
                Console.WriteLine($"Результат: {resultTxt.BestDistance:F2}");
                Console.WriteLine($"Маршрут: {string.Join(" → ", resultTxt.BestRoute.Take(5))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка завантаження з TXT: {ex.Message}");
            }
            
            // Тест завантаження з JSON файлу
            try
            {
                Console.WriteLine("\nТест завантаження з small_grid_16.json:");
                var citiesFromJson = CityLoader.LoadFromJsonFile("Examples/TestData/small_grid_16.json");
                Console.WriteLine($"Завантажено {citiesFromJson.Count} міст");
                
                var resultJson = RunGeneticAlgorithm(citiesFromJson, options);
                Console.WriteLine($"Результат: {resultJson.BestDistance:F2}");
                Console.WriteLine($"Маршрут: {string.Join(" → ", resultJson.BestRoute.Take(5))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка завантаження з JSON: {ex.Message}");
            }
        }
        
        private static void TestCityPatterns()
        {
            Console.WriteLine("3. Тестування різних патернів міст");
            Console.WriteLine("===================================");
            
            var options = new ModuleOptions
            {
                CitiesNumber = 36,
                PopulationSize = 400,
                Generations = 40,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            var patterns = new[] 
            {
                TestCityPattern.Random,
                TestCityPattern.Grid,
                TestCityPattern.Clustered,
                TestCityPattern.Circle
            };
            
            foreach (var pattern in patterns)
            {
                Console.WriteLine($"\nТест патерну: {pattern}");
                Console.WriteLine(new string('-', 30));
                
                var cities = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, pattern);
                var stopwatch = Stopwatch.StartNew();
                var result = RunGeneticAlgorithm(cities, options);
                stopwatch.Stop();
                
                Console.WriteLine($"Кількість міст: {cities.Count}");
                Console.WriteLine($"Найкраща відстань: {result.BestDistance:F2}");
                Console.WriteLine($"Середня відстань: {result.AverageDistance:F2}");
                Console.WriteLine($"Поколінь виконано: {result.GenerationsCompleted}");
                Console.WriteLine($"Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} сек");
                
                // Аналіз геометрії
                AnalyzeCityGeometry(cities);
            }
        }
        
        private static void AnalyzeCityGeometry(List<City> cities)
        {
            var minX = cities.Min(c => c.X);
            var maxX = cities.Max(c => c.X);
            var minY = cities.Min(c => c.Y);
            var maxY = cities.Max(c => c.Y);
            
            var width = maxX - minX;
            var height = maxY - minY;
            var area = width * height;
            
            Console.WriteLine($"Геометрія: {width:F0} x {height:F0} (площа: {area:F0})");
            
            // Розрахунок середньої відстані між сусідніми містами
            var distances = new List<double>();
            for (int i = 0; i < cities.Count; i++)
            {
                for (int j = i + 1; j < cities.Count; j++)
                {
                    distances.Add(cities[i].DistanceTo(cities[j]));
                }
            }
            
            var avgDistance = distances.Average();
            var minDistance = distances.Min();
            var maxDistance = distances.Max();
            
            Console.WriteLine($"Відстані: середня={avgDistance:F1}, мін={minDistance:F1}, макс={maxDistance:F1}");
        }
        
        private static ModuleOutput RunGeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            var ga = new GeneticAlgorithm(cities, options);
            ga.Initialize();
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
        
        private static Route NearestNeighborHeuristic(List<City> cities)
        {
            var unvisited = new HashSet<City>(cities);
            var route = new List<int>();
            var current = cities[0];
            unvisited.Remove(current);
            route.Add(current.Id);
            
            while (unvisited.Count > 0)
            {
                var nearest = unvisited.OrderBy(c => current.DistanceTo(c)).First();
                route.Add(nearest.Id);
                unvisited.Remove(nearest);
                current = nearest;
            }
            
            return new Route(cities, new Random()) { Cities = route };
        }
    }
} 