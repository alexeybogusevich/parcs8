using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public static class CityLoader
    {
        /// <summary>
        /// Завантажує список міст з текстового файлу
        /// Формат: кожен рядок містить "ID X Y" (наприклад: "0 10.5 20.3")
        /// </summary>
        public static List<City> LoadFromTextFile(string filePath)
        {
            var cities = new List<City>();
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл з містами не знайдено: {filePath}");
            }
            
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;
                
                try
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        int id = int.Parse(parts[0]);
                        double x = double.Parse(parts[1]);
                        double y = double.Parse(parts[2]);
                        
                        cities.Add(new City(id, x, y));
                    }
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Помилка парсингу рядка {i + 1}: {line}. {ex.Message}");
                }
            }
            
            if (cities.Count == 0)
            {
                throw new InvalidDataException("Файл не містить валідних даних про міста");
            }
            
            return cities;
        }
        
        /// <summary>
        /// Завантажує список міст з JSON файлу
        /// </summary>
        public static List<City> LoadFromJsonFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"JSON файл з містами не знайдено: {filePath}");
            }
            
            var jsonContent = File.ReadAllText(filePath);
            var cities = JsonSerializer.Deserialize<List<City>>(jsonContent);
            
            if (cities == null || cities.Count == 0)
            {
                throw new InvalidDataException("JSON файл не містить валідних даних про міста");
            }
            
            return cities;
        }
        
        /// <summary>
        /// Зберігає список міст у текстовому форматі
        /// </summary>
        public static void SaveToTextFile(List<City> cities, string filePath)
        {
            var lines = new List<string>();
            lines.Add("# Формат: ID X Y");
            lines.Add("# Кожен рядок представляє одне місто");
            
            foreach (var city in cities.OrderBy(c => c.Id))
            {
                lines.Add($"{city.Id} {city.X:F2} {city.Y:F2}");
            }
            
            File.WriteAllLines(filePath, lines);
        }
        
        /// <summary>
        /// Зберігає список міст у JSON форматі
        /// </summary>
        public static void SaveToJsonFile(List<City> cities, string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var jsonContent = JsonSerializer.Serialize(cities, options);
            File.WriteAllText(filePath, jsonContent);
        }
        
        /// <summary>
        /// Генерує тестові міста з детерміністичним розподілом
        /// </summary>
        public static List<City> GenerateTestCities(int count, int seed, TestCityPattern pattern = TestCityPattern.Random)
        {
            var random = new Random(seed);
            var cities = new List<City>();
            
            switch (pattern)
            {
                case TestCityPattern.Random:
                    for (int i = 0; i < count; i++)
                    {
                        double x = random.NextDouble() * 1000;
                        double y = random.NextDouble() * 1000;
                        cities.Add(new City(i, x, y));
                    }
                    break;
                    
                case TestCityPattern.Grid:
                    int gridSize = (int)Math.Ceiling(Math.Sqrt(count));
                    int cityIndex = 0;
                    
                    for (int row = 0; row < gridSize && cityIndex < count; row++)
                    {
                        for (int col = 0; col < gridSize && cityIndex < count; col++)
                        {
                            double x = col * 100.0;
                            double y = row * 100.0;
                            cities.Add(new City(cityIndex, x, y));
                            cityIndex++;
                        }
                    }
                    break;
                    
                case TestCityPattern.Clustered:
                    int clusters = Math.Max(1, count / 10);
                    var clusterCenters = new List<(double, double)>();
                    
                    // Генеруємо центри кластерів
                    for (int i = 0; i < clusters; i++)
                    {
                        double centerX = random.NextDouble() * 800 + 100;
                        double centerY = random.NextDouble() * 800 + 100;
                        clusterCenters.Add((centerX, centerY));
                    }
                    
                    // Розподіляємо міста по кластерах
                    for (int i = 0; i < count; i++)
                    {
                        var cluster = clusterCenters[i % clusters];
                        double x = cluster.Item1 + (random.NextDouble() - 0.5) * 200;
                        double y = cluster.Item2 + (random.NextDouble() - 0.5) * 200;
                        
                        // Обмежуємо координати
                        x = Math.Max(0, Math.Min(1000, x));
                        y = Math.Max(0, Math.Min(1000, y));
                        
                        cities.Add(new City(i, x, y));
                    }
                    break;
                    
                case TestCityPattern.Circle:
                    double radius = 400;
                    double centerX = 500;
                    double centerY = 500;
                    
                    for (int i = 0; i < count; i++)
                    {
                        double angle = (2 * Math.PI * i) / count;
                        double x = centerX + radius * Math.Cos(angle);
                        double y = centerY + radius * Math.Sin(angle);
                        cities.Add(new City(i, x, y));
                    }
                    break;
            }
            
            return cities;
        }
    }
    
    public enum TestCityPattern
    {
        Random,     // Випадковий розподіл
        Grid,       // Сітка
        Clustered,  // Кластеризований
        Circle      // Коло
    }
} 