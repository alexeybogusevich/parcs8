using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Examples
{
    public class GenerateTestData
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Генератор тестових даних для TSP ===\n");
            
            // Створюємо директорію для тестових даних
            var testDataDir = "Examples/TestData";
            Directory.CreateDirectory(testDataDir);
            
            // Генеруємо різноманітні тестові задачі
            GenerateStandardTestSets(testDataDir);
            GenerateBenchmarkInstances(testDataDir);
            GenerateSpecialCases(testDataDir);
            
            Console.WriteLine("\n=== Генерація завершена ===");
            Console.WriteLine($"Всі файли збережено в директорії: {testDataDir}");
        }
        
        private static void GenerateStandardTestSets(string outputDir)
        {
            Console.WriteLine("1. Генерація стандартних тестових наборів");
            
            // Маленькі задачі (для швидкого тестування)
            GenerateTestSet(outputDir, "tiny_random_10", 10, TestCityPattern.Random, 42);
            GenerateTestSet(outputDir, "tiny_grid_9", 9, TestCityPattern.Grid, 42);
            GenerateTestSet(outputDir, "tiny_clustered_12", 12, TestCityPattern.Clustered, 42);
            
            // Середні задачі (для основного тестування)
            GenerateTestSet(outputDir, "medium_random_64", 64, TestCityPattern.Random, 42);
            GenerateTestSet(outputDir, "medium_grid_64", 64, TestCityPattern.Grid, 42);
            GenerateTestSet(outputDir, "medium_clustered_60", 60, TestCityPattern.Clustered, 42);
            GenerateTestSet(outputDir, "medium_circle_64", 64, TestCityPattern.Circle, 42);
            
            // Великі задачі (для тестування продуктивності)
            GenerateTestSet(outputDir, "large_random_144", 144, TestCityPattern.Random, 42);
            GenerateTestSet(outputDir, "large_grid_144", 144, TestCityPattern.Grid, 42);
            GenerateTestSet(outputDir, "large_clustered_150", 150, TestCityPattern.Clustered, 42);
        }
        
        private static void GenerateBenchmarkInstances(string outputDir)
        {
            Console.WriteLine("2. Генерація benchmark інстансів");
            
            // Відомі тестові задачі з літератури
            GenerateEuclideanTSP(outputDir, "eil51", 51, 42);
            GenerateEuclideanTSP(outputDir, "eil76", 76, 42);
            GenerateEuclideanTSP(outputDir, "eil101", 101, 42);
            
            // Задачі з симетричними патернами
            GenerateSymmetricPattern(outputDir, "symmetric_50", 50, 42);
            GenerateSymmetricPattern(outputDir, "symmetric_100", 100, 42);
        }
        
        private static void GenerateSpecialCases(string outputDir)
        {
            Console.WriteLine("3. Генерація спеціальних випадків");
            
            // Задачі з дуже близькими містами
            GenerateClusteredCities(outputDir, "tight_clusters_80", 80, 42);
            
            // Задачі з дуже розрідженими містами
            GenerateSparseCities(outputDir, "sparse_100", 100, 42);
            
            // Задачі з містами на лінії
            GenerateLinearCities(outputDir, "linear_50", 50, 42);
            
            // Задачі з містами в формі спіралі
            GenerateSpiralCities(outputDir, "spiral_75", 75, 42);
        }
        
        private static void GenerateTestSet(string outputDir, string name, int cityCount, TestCityPattern pattern, int seed)
        {
            var cities = CityLoader.GenerateTestCities(cityCount, seed, pattern);
            
            // Зберігаємо у текстовому форматі
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            // Зберігаємо у JSON форматі
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст ({pattern})");
        }
        
        private static void GenerateEuclideanTSP(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Генеруємо міста з координатами 0-1000
            for (int i = 0; i < cityCount; i++)
            {
                double x = random.NextDouble() * 1000;
                double y = random.NextDouble() * 1000;
                cities.Add(new City(i, x, y));
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст (Euclidean)");
        }
        
        private static void GenerateSymmetricPattern(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Створюємо симетричний патерн
            int half = cityCount / 2;
            for (int i = 0; i < half; i++)
            {
                double angle = (2 * Math.PI * i) / half;
                double radius = 200 + random.NextDouble() * 300;
                double x = 500 + radius * Math.Cos(angle);
                double y = 500 + radius * Math.Sin(angle);
                cities.Add(new City(i, x, y));
                
                // Додаємо симетричну точку
                if (i > 0 && i < half - 1)
                {
                    double symAngle = angle + Math.PI;
                    double symX = 500 + radius * Math.Cos(symAngle);
                    double symY = 500 + radius * Math.Sin(symAngle);
                    cities.Add(new City(half + i, symX, symY));
                }
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cities.Count} міст (Symmetric)");
        }
        
        private static void GenerateClusteredCities(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Створюємо дуже тісні кластери
            int clusters = Math.Max(1, cityCount / 20);
            var clusterCenters = new List<(double, double)>();
            
            for (int i = 0; i < clusters; i++)
            {
                double centerX = random.NextDouble() * 800 + 100;
                double centerY = random.NextDouble() * 800 + 100;
                clusterCenters.Add((centerX, centerY));
            }
            
            for (int i = 0; i < cityCount; i++)
            {
                var cluster = clusterCenters[i % clusters];
                double x = cluster.Item1 + (random.NextDouble() - 0.5) * 50; // Дуже тісні кластери
                double y = cluster.Item2 + (random.NextDouble() - 0.5) * 50;
                
                x = Math.Max(0, Math.Min(1000, x));
                y = Math.Max(0, Math.Min(1000, y));
                
                cities.Add(new City(i, x, y));
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст (Tight Clusters)");
        }
        
        private static void GenerateSparseCities(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Створюємо дуже розріджені міста
            for (int i = 0; i < cityCount; i++)
            {
                double x = random.NextDouble() * 10000; // Великий діапазон
                double y = random.NextDouble() * 10000;
                cities.Add(new City(i, x, y));
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст (Sparse)");
        }
        
        private static void GenerateLinearCities(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Міста розташовані вздовж лінії з невеликими відхиленнями
            for (int i = 0; i < cityCount; i++)
            {
                double x = i * 20.0; // Лінійне розташування по X
                double y = 500 + (random.NextDouble() - 0.5) * 100; // Невеликі відхилення по Y
                cities.Add(new City(i, x, y));
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст (Linear)");
        }
        
        private static void GenerateSpiralCities(string outputDir, string name, int cityCount, int seed)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            // Міста розташовані по спіралі
            for (int i = 0; i < cityCount; i++)
            {
                double angle = (2 * Math.PI * i) / cityCount * 3; // 3 обороти
                double radius = 50 + (i * 200.0 / cityCount); // Зростаючий радіус
                double x = 500 + radius * Math.Cos(angle);
                double y = 500 + radius * Math.Sin(angle);
                
                // Додаємо невеликі випадкові відхилення
                x += (random.NextDouble() - 0.5) * 20;
                y += (random.NextDouble() - 0.5) * 20;
                
                cities.Add(new City(i, x, y));
            }
            
            var txtPath = Path.Combine(outputDir, $"{name}.txt");
            CityLoader.SaveToTextFile(cities, txtPath);
            
            var jsonPath = Path.Combine(outputDir, $"{name}.json");
            CityLoader.SaveToJsonFile(cities, jsonPath);
            
            Console.WriteLine($"  ✓ {name}: {cityCount} міст (Spiral)");
        }
    }
} 