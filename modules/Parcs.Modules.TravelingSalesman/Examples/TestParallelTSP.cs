using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Examples
{
    /// <summary>
    /// Простий тест для перевірки паралельного TSP модуля
    /// </summary>
    public class TestParallelTSP
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Тест паралельного TSP модуля ===");
            
            try
            {
                // Створюємо тестові міста
                var cities = CityLoader.GenerateTestCities(100, 42, TestCityPattern.Random);
                Console.WriteLine($"Створено {cities.Count} тестових міст");
                
                // Створюємо опції
                var options = new ModuleOptions
                {
                    CitiesNumber = cities.Count,
                    PopulationSize = 200,
                    Generations = 50,
                    MutationRate = 0.01,
                    CrossoverRate = 0.8,
                    PointsNumber = 2, // 2 точки для паралельної обробки
                    SaveResults = false,
                    Seed = 42
                };
                
                Console.WriteLine($"Параметри: Population={options.PopulationSize}, Generations={options.Generations}, Points={options.PointsNumber}");
                
                // Тестуємо послідовний алгоритм для порівняння
                Console.WriteLine("\n--- Тестування послідовного алгоритму ---");
                var sequentialResult = TestSequentialAlgorithm(cities, options);
                
                Console.WriteLine($"Послідовний результат: {sequentialResult.BestDistance:F2}");
                Console.WriteLine($"Час виконання: {sequentialResult.ElapsedSeconds:F2} сек");
                
                // Тестуємо паралельний алгоритм
                Console.WriteLine("\n--- Тестування паралельного алгоритму ---");
                var parallelResult = TestParallelAlgorithm(cities, options);
                
                Console.WriteLine($"Паралельний результат: {parallelResult.BestDistance:F2}");
                Console.WriteLine($"Час виконання: {parallelResult.ElapsedSeconds:F2} сек");
                
                // Порівняння результатів
                Console.WriteLine("\n--- Порівняння результатів ---");
                var speedup = sequentialResult.ElapsedSeconds / parallelResult.ElapsedSeconds;
                var qualityRatio = parallelResult.BestDistance / sequentialResult.BestDistance;
                
                Console.WriteLine($"Прискорення: {speedup:F2}x");
                Console.WriteLine($"Якість результату: {qualityRatio:F3} (1.0 = ідентична якість)");
                
                if (speedup > 1.0)
                {
                    Console.WriteLine("✓ Паралельний алгоритм швидший");
                }
                else
                {
                    Console.WriteLine("⚠ Паралельний алгоритм не показав прискорення");
                }
                
                if (Math.Abs(qualityRatio - 1.0) < 0.1)
                {
                    Console.WriteLine("✓ Якість результатів подібна");
                }
                else
                {
                    Console.WriteLine("⚠ Різна якість результатів");
                }
                
                Console.WriteLine("\n=== Тест завершено успішно! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Помилка під час тестування: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static ModuleOutput TestSequentialAlgorithm(List<City> cities, ModuleOptions options)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var ga = new GeneticAlgorithm(cities, options);
            ga.Initialize();
            ga.RunGenerations(options.Generations);
            
            stopwatch.Stop();
            
            var bestRoute = ga.GetBestRoute();
            var averageDistance = ga.GetAverageDistance();
            var convergenceHistory = ga.GetConvergenceHistory();
            
            return new ModuleOutput
            {
                BestDistance = bestRoute.TotalDistance,
                AverageDistance = averageDistance,
                GenerationsCompleted = convergenceHistory.Count,
                BestRoute = bestRoute.Cities,
                ConvergenceHistory = convergenceHistory,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
        
        private static ModuleOutput TestParallelAlgorithm(List<City> cities, ModuleOptions options)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Симулюємо паралельну обробку
            var localOptions = new ModuleOptions
            {
                CitiesNumber = options.CitiesNumber,
                PopulationSize = options.PopulationSize / options.PointsNumber,
                Generations = options.Generations,
                MutationRate = options.MutationRate,
                CrossoverRate = options.CrossoverRate,
                PointsNumber = 1,
                SaveResults = false,
                Seed = options.Seed
            };
            
            var results = new List<ModuleOutput>();
            
            // Запускаємо кілька локальних GA з різними seed
            for (int i = 0; i < options.PointsNumber; i++)
            {
                var workerOptions = new ModuleOptions
                {
                    CitiesNumber = localOptions.CitiesNumber,
                    PopulationSize = localOptions.PopulationSize,
                    Generations = localOptions.Generations,
                    MutationRate = localOptions.MutationRate,
                    CrossoverRate = localOptions.CrossoverRate,
                    PointsNumber = 1,
                    SaveResults = false,
                    Seed = localOptions.Seed + i
                };
                
                var ga = new GeneticAlgorithm(cities, workerOptions);
                ga.Initialize();
                ga.RunGenerations(workerOptions.Generations);
                
                var bestRoute = ga.GetBestRoute();
                var averageDistance = ga.GetAverageDistance();
                var convergenceHistory = ga.GetConvergenceHistory();
                
                results.Add(new ModuleOutput
                {
                    BestDistance = bestRoute.TotalDistance,
                    AverageDistance = averageDistance,
                    GenerationsCompleted = convergenceHistory.Count,
                    BestRoute = bestRoute.Cities,
                    ConvergenceHistory = convergenceHistory
                });
            }
            
            stopwatch.Stop();
            
            // Об'єднуємо результати
            var bestResult = results.OrderBy(r => r.BestDistance).First();
            var combinedHistory = CombineConvergenceHistories(results);
            
            return new ModuleOutput
            {
                BestDistance = bestResult.BestDistance,
                AverageDistance = results.Average(r => r.AverageDistance),
                GenerationsCompleted = results.Max(r => r.GenerationsCompleted),
                BestRoute = bestResult.BestRoute,
                ConvergenceHistory = combinedHistory,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
        
        private static List<double> CombineConvergenceHistories(List<ModuleOutput> results)
        {
            var combined = new List<double>();
            var maxGenerations = results.Max(r => r.ConvergenceHistory.Count);
            
            for (int gen = 0; gen < maxGenerations; gen++)
            {
                var generationValues = new List<double>();
                foreach (var result in results)
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
    }
} 