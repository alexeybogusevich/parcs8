using System.Diagnostics;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Examples
{
    public class Demo
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Демонстрація нових можливостей TSP модуля ===\n");
            
            // Демонстрація 1: Завантаження з файлу
            DemoFileLoading();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            // Демонстрація 2: Детерміністичні результати
            DemoDeterministicResults();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            // Демонстрація 3: Різні патерни міст
            DemoCityPatterns();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            // Демонстрація 4: Порівняння послідовного та паралельного підходів
            DemoSequentialVsParallel();
            
            Console.WriteLine("\n=== Демонстрація завершена ===");
        }
        
        private static void DemoFileLoading()
        {
            Console.WriteLine("1. ДЕМОНСТРАЦІЯ ЗАВАНТАЖЕННЯ З ФАЙЛУ");
            Console.WriteLine("=====================================");
            
            var options = new ModuleOptions
            {
                PopulationSize = 300,
                Generations = 30,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            // Тестуємо різні формати файлів
            var testFiles = new[]
            {
                "Examples/TestData/small_grid_16.txt",
                "Examples/TestData/small_grid_16.json",
                "Examples/TestData/medium_clustered_50.txt"
            };
            
            foreach (var file in testFiles)
            {
                try
                {
                    Console.WriteLine($"\nЗавантаження з файлу: {file}");
                    
                    List<City> cities;
                    if (file.EndsWith(".json"))
                    {
                        cities = CityLoader.LoadFromJsonFile(file);
                    }
                    else
                    {
                        cities = CityLoader.LoadFromTextFile(file);
                    }
                    
                    Console.WriteLine($"  ✓ Завантажено {cities.Count} міст");
                    
                    // Аналіз геометрії
                    var minX = cities.Min(c => c.X);
                    var maxX = cities.Max(c => c.X);
                    var minY = cities.Min(c => c.Y);
                    var maxY = cities.Max(c => c.Y);
                    
                    Console.WriteLine($"  ✓ Координати: X[{minX:F0}, {maxX:F0}], Y[{minY:F0}, {maxY:F0}]");
                    
                    // Швидкий тест GA
                    var stopwatch = Stopwatch.StartNew();
                    var result = RunQuickGA(cities, options);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  ✓ Результат: {result.BestDistance:F2} за {stopwatch.Elapsed.TotalSeconds:F2}с");
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Помилка: {ex.Message}");
                }
            }
        }
        
        private static void DemoDeterministicResults()
        {
            Console.WriteLine("2. ДЕМОНСТРАЦІЯ ДЕТЕРМІНІСТИЧНИХ РЕЗУЛЬТАТІВ");
            Console.WriteLine("=============================================");
            
            var options = new ModuleOptions
            {
                CitiesNumber = 20,
                PopulationSize = 200,
                Generations = 25,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            Console.WriteLine($"\nТестування з seed={options.Seed}");
            
            // Перший запуск
            var cities1 = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
            var result1 = RunQuickGA(cities1, options);
            Console.WriteLine($"  Запуск 1: {result1.BestDistance:F2}");
            
            // Другий запуск з тим самим seed
            var cities2 = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
            var result2 = RunQuickGA(cities2, options);
            Console.WriteLine($"  Запуск 2: {result2.BestDistance:F2}");
            
            // Перевірка детермінізму
            if (Math.Abs(result1.BestDistance - result2.BestDistance) < 0.01)
            {
                Console.WriteLine($"  ✓ Детермінізм забезпечено (різниця: {Math.Abs(result1.BestDistance - result2.BestDistance):F4})");
            }
            else
            {
                Console.WriteLine($"  ✗ Проблема з детермінізмом (різниця: {Math.Abs(result1.BestDistance - result2.BestDistance):F4})");
            }
            
            // Тест з різними seed
            Console.WriteLine($"\nТестування з різними seed:");
            var seeds = new[] { 42, 123, 456, 789 };
            var results = new List<double>();
            
            foreach (var seed in seeds)
            {
                var cities = CityLoader.GenerateTestCities(options.CitiesNumber, seed, TestCityPattern.Random);
                var result = RunQuickGA(cities, options);
                results.Add(result.BestDistance);
                Console.WriteLine($"  Seed {seed}: {result.BestDistance:F2}");
            }
            
            var avgResult = results.Average();
            var stdDev = Math.Sqrt(results.Select(r => Math.Pow(r - avgResult, 2)).Average());
            Console.WriteLine($"  Середнє: {avgResult:F2}, Стандартне відхилення: {stdDev:F2}");
        }
        
        private static void DemoCityPatterns()
        {
            Console.WriteLine("3. ДЕМОНСТРАЦІЯ РІЗНИХ ПАТЕРНІВ МІСТ");
            Console.WriteLine("=====================================");
            
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
                Console.WriteLine($"\nПатерн: {pattern}");
                Console.WriteLine(new string('-', 30));
                
                var cities = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, pattern);
                var stopwatch = Stopwatch.StartNew();
                var result = RunQuickGA(cities, options);
                stopwatch.Stop();
                
                Console.WriteLine($"  Кількість міст: {cities.Count}");
                Console.WriteLine($"  Найкраща відстань: {result.BestDistance:F2}");
                Console.WriteLine($"  Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} сек");
                
                // Аналіз геометрії
                var minX = cities.Min(c => c.X);
                var maxX = cities.Max(c => c.X);
                var minY = cities.Min(c => c.Y);
                var maxY = cities.Max(c => c.Y);
                
                var width = maxX - minX;
                var height = maxY - minY;
                var area = width * height;
                
                Console.WriteLine($"  Геометрія: {width:F0} x {height:F0} (площа: {area:F0})");
                
                // Розрахунок середньої відстані між містами
                var distances = new List<double>();
                for (int i = 0; i < cities.Count; i++)
                {
                    for (int j = i + 1; j < cities.Count; j++)
                    {
                        distances.Add(cities[i].DistanceTo(cities[j]));
                    }
                }
                
                var avgDistance = distances.Average();
                Console.WriteLine($"  Середня відстань між містами: {avgDistance:F1}");
            }
        }
        
        private static void DemoSequentialVsParallel()
        {
            Console.WriteLine("4. ДЕМОНСТРАЦІЯ ПОРІВНЯННЯ ПОСЛІДОВНОГО ТА ПАЛЕЛЬНОГО ПІДХОДІВ");
            Console.WriteLine("===============================================================");
            
            var options = new ModuleOptions
            {
                CitiesNumber = 64,
                PopulationSize = 800,
                Generations = 60,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42,
                SaveResults = false
            };
            
            Console.WriteLine($"\nТестування з {options.CitiesNumber} містами");
            Console.WriteLine($"Параметри: Population={options.PopulationSize}, Generations={options.Generations}");
            
            var cities = CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Clustered);
            
            // Послідовне виконання
            Console.WriteLine("\nПослідовне виконання:");
            var seqStopwatch = Stopwatch.StartNew();
            var seqResult = RunQuickGA(cities, options);
            seqStopwatch.Stop();
            
            Console.WriteLine($"  Найкраща відстань: {seqResult.BestDistance:F2}");
            Console.WriteLine($"  Час виконання: {seqStopwatch.Elapsed.TotalSeconds:F2} сек");
            
            // Симуляція паралельного виконання (розподіл популяції)
            Console.WriteLine("\nСимуляція паралельного виконання (4 точки):");
            var parallelOptions = new ModuleOptions
            {
                CitiesNumber = options.CitiesNumber,
                PopulationSize = options.PopulationSize / 4, // Розподіл популяції
                Generations = options.Generations,
                MutationRate = options.MutationRate,
                CrossoverRate = options.CrossoverRate,
                Seed = options.Seed,
                SaveResults = false
            };
            
            var parallelResults = new List<ModuleOutput>();
            var parallelStopwatch = Stopwatch.StartNew();
            
            // Симулюємо 4 паралельні точки
            for (int i = 0; i < 4; i++)
            {
                var workerCities = new List<City>(cities);
                var workerSeed = options.Seed + i; // Різні seed для різноманітності
                var workerOptions = new ModuleOptions
                {
                    CitiesNumber = parallelOptions.CitiesNumber,
                    PopulationSize = parallelOptions.PopulationSize,
                    Generations = parallelOptions.Generations,
                    MutationRate = parallelOptions.MutationRate,
                    CrossoverRate = parallelOptions.CrossoverRate,
                    Seed = workerSeed,
                    SaveResults = false
                };
                
                var workerResult = RunQuickGA(workerCities, workerOptions);
                parallelResults.Add(workerResult);
            }
            
            parallelStopwatch.Stop();
            
            // Знаходимо найкращий результат
            var bestParallelResult = parallelResults.OrderBy(r => r.BestDistance).First();
            var avgParallelResult = parallelResults.Average(r => r.BestDistance);
            
            Console.WriteLine($"  Найкраща відстань: {bestParallelResult.BestDistance:F2}");
            Console.WriteLine($"  Середня відстань: {avgParallelResult:F2}");
            Console.WriteLine($"  Час виконання: {parallelStopwatch.Elapsed.TotalSeconds:F2} сек");
            
            // Порівняння
            Console.WriteLine("\nПорівняння:");
            var distanceImprovement = ((seqResult.BestDistance - bestParallelResult.BestDistance) / seqResult.BestDistance * 100);
            var timeRatio = seqStopwatch.Elapsed.TotalSeconds / parallelStopwatch.Elapsed.TotalSeconds;
            
            Console.WriteLine($"  Покращення відстані: {distanceImprovement:F1}%");
            Console.WriteLine($"  Прискорення часу: {timeRatio:F1}x");
            
            if (distanceImprovement > 0)
            {
                Console.WriteLine($"  ✓ Паралельний підхід знайшов краще рішення");
            }
            else
            {
                Console.WriteLine($"  ✗ Послідовний підхід знайшов краще рішення");
            }
            
            if (timeRatio > 1)
            {
                Console.WriteLine($"  ✓ Паралельний підхід швидший");
            }
            else
            {
                Console.WriteLine($"  ✗ Послідовний підхід швидший");
            }
        }
        
        private static ModuleOutput RunQuickGA(List<City> cities, ModuleOptions options)
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
    }
} 