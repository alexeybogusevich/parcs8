// Simple script to generate 100k cities file
// Usage: dotnet script Generate100k.cs

using System.Diagnostics;

const int cityCount = 100000;
const int seed = 42;
const string outputFile = "cities_100k.txt";

Console.WriteLine($"Generating {cityCount:N0} cities...");
Console.WriteLine($"Output file: {outputFile}");

var stopwatch = Stopwatch.StartNew();
var random = new Random(seed);

// Generate and write directly to file (streaming to save memory)
using var writer = new StreamWriter(outputFile);
writer.WriteLine("# Format: ID X Y");
writer.WriteLine("# Each line represents one city");
writer.WriteLine($"# Total cities: {cityCount:N0}");

for (int i = 0; i < cityCount; i++)
{
    double x = random.NextDouble() * 1000;
    double y = random.NextDouble() * 1000;
    writer.WriteLine($"{i} {x:F2} {y:F2}");
    
    if ((i + 1) % 10000 == 0)
    {
        Console.WriteLine($"  Generated {i + 1:N0} cities...");
    }
}

stopwatch.Stop();
Console.WriteLine($"Generated {cityCount:N0} cities in {stopwatch.ElapsedMilliseconds} ms");

var fileInfo = new FileInfo(outputFile);
Console.WriteLine($"File size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
Console.WriteLine("Done!");

