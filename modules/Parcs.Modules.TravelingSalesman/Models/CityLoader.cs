using System.Text.Json;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public static class CityLoader
    {
        /// <summary>
        /// Loads a list of cities from a text file.
        /// Format: each line contains "ID X Y" (e.g. "0 10.5 20.3").
        /// </summary>
        public static List<City> LoadFromTextFile(string filePath)
        {
            var cities = new List<City>();
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"City file not found: {filePath}");
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
                    throw new FormatException($"Error parsing line {i + 1}: {line}. {ex.Message}");
                }
            }
            
            if (cities.Count == 0)
            {
                throw new InvalidDataException("File contains no valid city data");
            }
            
            return cities;
        }

        /// <summary>
        /// Loads a list of cities from a text stream.
        /// Format: each line contains "ID X Y" (e.g. "0 10.5 20.3").
        /// </summary>
        public static List<City> LoadFromTextFile(Stream stream)
        {
            var cities = new List<City>();
            using var reader = new StreamReader(stream);
            
            string? line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;
                
                try
                {
                    var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
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
                    throw new FormatException($"Error parsing line {lineNumber}: {line}. {ex.Message}");
                }
            }
            
            if (cities.Count == 0)
            {
                throw new InvalidDataException("Stream contains no valid city data");
            }
            
            return cities;
        }
        
        /// <summary>
        /// Loads a list of cities from a JSON file.
        /// </summary>
        public static List<City> LoadFromJsonFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"JSON city file not found: {filePath}");
            }
            
            var jsonContent = File.ReadAllText(filePath);
            var cities = JsonSerializer.Deserialize<List<City>>(jsonContent);
            
            if (cities == null || cities.Count == 0)
            {
                throw new InvalidDataException("JSON file contains no valid city data");
            }
            
            return cities;
        }

        /// <summary>
        /// Loads a list of cities from a JSON stream.
        /// </summary>
        public static List<City> LoadFromJsonFile(Stream stream)
        {
            var cities = JsonSerializer.Deserialize<List<City>>(stream);
            
            if (cities == null || cities.Count == 0)
            {
                throw new InvalidDataException("JSON stream contains no valid city data");
            }
            
            return cities;
        }
        
        /// <summary>
        /// Saves a list of cities in text format.
        /// </summary>
        public static void SaveToTextFile(List<City> cities, string filePath)
        {
            var lines = new List<string>();
            lines.Add("# Format: ID X Y");
            lines.Add("# Each line represents one city");
            
            foreach (var city in cities.OrderBy(c => c.Id))
            {
                lines.Add($"{city.Id} {city.X:F2} {city.Y:F2}");
            }
            
            File.WriteAllLines(filePath, lines);
        }
        
        /// <summary>
        /// Saves a list of cities in JSON format.
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
        /// Generates test cities with a deterministic distribution.
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
                    
                    // Generate cluster centers
                    for (int i = 0; i < clusters; i++)
                    {
                        double centerX = random.NextDouble() * 800 + 100;
                        double centerY = random.NextDouble() * 800 + 100;
                        clusterCenters.Add((centerX, centerY));
                    }
                    
                    // Distribute cities among clusters
                    for (int i = 0; i < count; i++)
                    {
                        var cluster = clusterCenters[i % clusters];
                        double x = cluster.Item1 + (random.NextDouble() - 0.5) * 200;
                        double y = cluster.Item2 + (random.NextDouble() - 0.5) * 200;
                        
                        // Clamp coordinates
                        x = Math.Max(0, Math.Min(1000, x));
                        y = Math.Max(0, Math.Min(1000, y));
                        
                        cities.Add(new City(i, x, y));
                    }
                    break;
                    
                case TestCityPattern.Circle:
                    double circleRadius = 400;
                    double circleCenterX = 500;
                    double circleCenterY = 500;
                    
                    for (int i = 0; i < count; i++)
                    {
                        double angle = (2 * Math.PI * i) / count;
                        double x = circleCenterX + circleRadius * Math.Cos(angle);
                        double y = circleCenterY + circleRadius * Math.Sin(angle);
                        cities.Add(new City(i, x, y));
                    }
                    break;
            }
            
            return cities;
        }
    }
    
    public enum TestCityPattern
    {
        Random,     // Random distribution
        Grid,       // Grid layout
        Clustered,  // Clustered
        Circle      // Circle
    }
} 