using Parcs.Modules.TravelingSalesman.Models;

namespace GenerateCities100k;

class Program
{
    static void Main(string[] args)
    {
        const int cityCount = 100000;
        const int seed = 42; // Deterministic generation
        const string outputFile = "cities_100k.txt";
        
        Console.WriteLine($"Generating {cityCount:N0} cities...");
        Console.WriteLine($"Output file: {outputFile}");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Generate cities
        var cities = CityLoader.GenerateTestCities(cityCount, seed, TestCityPattern.Random);
        
        Console.WriteLine($"Generated {cities.Count:N0} cities in {stopwatch.ElapsedMilliseconds} ms");
        
        // Save to text file (more efficient than JSON for large datasets)
        Console.WriteLine($"Saving to {outputFile}...");
        stopwatch.Restart();
        
        // Use streaming write for better memory efficiency with large files
        SaveToTextFileStreaming(cities, outputFile);
        
        stopwatch.Stop();
        Console.WriteLine($"Saved in {stopwatch.ElapsedMilliseconds} ms");
        
        // Calculate file size
        var fileInfo = new FileInfo(outputFile);
        Console.WriteLine($"File size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
        
        Console.WriteLine("Done!");
    }
    
    private static void SaveToTextFileStreaming(List<City> cities, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        
        writer.WriteLine("# Format: ID X Y");
        writer.WriteLine("# Each line represents one city");
        writer.WriteLine($"# Total cities: {cities.Count:N0}");
        
        foreach (var city in cities.OrderBy(c => c.Id))
        {
            writer.WriteLine($"{city.Id} {city.X:F2} {city.Y:F2}");
        }
    }
}

