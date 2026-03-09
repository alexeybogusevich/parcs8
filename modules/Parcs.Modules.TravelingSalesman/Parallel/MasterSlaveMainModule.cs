using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Master-Slave parallel GA module with REAL speedup.
    /// Master maintains single population and does selection/crossover/mutation.
    /// Workers only calculate fitness (distance) in parallel.
    /// This gives true speedup because fitness calculation is the bottleneck.
    /// </summary>
    public class MasterSlaveMainModule : IModule
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = moduleInfo.BindModuleOptions<ModuleOptions>();
                var cities = await LoadOrGenerateCitiesAsync(moduleInfo, options);
                
                options.CitiesNumber = cities.Count;
                
                moduleInfo.Logger.LogInformation("Starting Master-Slave TSP module with {CitiesCount} cities", cities.Count);
                moduleInfo.Logger.LogInformation("Real speedup through parallel fitness evaluation");
                moduleInfo.Logger.LogInformation("Parameters: Population={Population}, Generations={Generations}, Workers={Workers}, Mutation={Mutation:F3}",
                    options.PopulationSize, options.Generations, options.PointsNumber, options.MutationRate);
                
                var stopwatch = Stopwatch.StartNew();

                // ── Phase 1: Pod provisioning ──────────────────────────────────────────
                // Create worker points for parallel fitness evaluation.
                // MasterSlave creates them sequentially (one SB message + wait per pod).
                var channels = new IChannel[options.PointsNumber];
                var points = new IPoint[options.PointsNumber];

                for (int i = 0; i < options.PointsNumber; ++i)
                {
                    points[i] = await moduleInfo.CreatePointAsync();
                    channels[i] = await points[i].CreateChannelAsync();
                }
                // All daemons are connected — record infrastructure overhead.
                var podProvisioningSeconds = stopwatch.Elapsed.TotalSeconds;

                moduleInfo.Logger.LogInformation("Created {WorkersCount} workers for parallel fitness evaluation", points.Length);

                // ── Phase 2: Computation ───────────────────────────────────────────────
                foreach (var point in points)
                {
                    try
                    {
                        await point.ExecuteClassAsync<MasterSlaveWorkerModule>();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogError(ex, "Error starting worker module: {Message}", ex.Message);
                        throw;
                    }
                }

                // Send cities to all workers (needed for distance calculation).
                for (int i = 0; i < points.Length; ++i)
                {
                    await WriteCitiesBinaryAsync(channels[i], cities);
                }

                moduleInfo.Logger.LogInformation("City data sent to workers; starting GA with parallel fitness evaluation");

                // Run Master-Slave GA
                var result = await RunMasterSlaveGA(moduleInfo, cities, options, channels);

                stopwatch.Stop();
                result.ElapsedSeconds         = stopwatch.Elapsed.TotalSeconds;
                result.PodProvisioningSeconds = podProvisioningSeconds;
                result.ComputeSeconds         = result.ElapsedSeconds - podProvisioningSeconds;

                if (options.SaveResults)
                {
                    await SaveResultsAsync(moduleInfo, cities, result, options);
                }

                var jsonContent = JsonSerializer.Serialize(result, JsonSerializerOptions);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent), 
                    options.OutputFile);
                
                moduleInfo.Logger.LogInformation("Master-Slave TSP completed in {ElapsedSeconds:F2} seconds", result.ElapsedSeconds);
                moduleInfo.Logger.LogInformation("Best distance: {BestDistance:F2}", result.BestDistance);
                
                // Cleanup
                foreach (var point in points)
                {
                    try
                    {
                        await point.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex, "Error deleting point: {Message}", ex.Message);
                    }
                }
                
                foreach (var channel in channels)
                {
                    try
                    {
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex, "Error closing channel: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Critical error in Master-Slave TSP module: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<ModuleOutput> RunMasterSlaveGA(
            IModuleInfo moduleInfo,
            List<City> cities,
            ModuleOptions options,
            IChannel[] workerChannels)
        {
            var random = new Random(options.Seed);
            var convergenceHistory = new List<double>();
            
            var population = new List<Route>();
            for (int i = 0; i < options.PopulationSize; i++)
            {
                // Create random permutation
                var permutation = Enumerable.Range(0, cities.Count).ToList();
                // Shuffle manually
                for (int j = permutation.Count - 1; j > 0; j--)
                {
                    var k = random.Next(j + 1);
                    (permutation[k], permutation[j]) = (permutation[j], permutation[k]);
                }

                population.Add(new(cities, random, permutation, skipDistanceCalculation: true));
            }
            
            // Calculate initial fitness in parallel
            await EvaluateFitnessParallel(moduleInfo, population, workerChannels);
            population = [.. population.OrderBy(r => r.TotalDistance)];

            var bestDistance = population[0].TotalDistance;
            convergenceHistory.Add(bestDistance);
            
            moduleInfo.Logger.LogInformation("Initial population: Best={Best:F2}, Avg={Avg:F2}", 
                bestDistance, population.Average(r => r.TotalDistance));

            // Evolve for specified generations
            for (int gen = 0; gen < options.Generations; gen++)
            {
                var newPopulation = new List<Route>
                {
                    // Elitism: keep best route (preserve distance for best route)
                    new(population[0])
                };
                
                // Create new individuals through selection, crossover, mutation
                // Skip distance calculation - will be done in parallel
                while (newPopulation.Count < options.PopulationSize)
                {
                    // Selection (tournament of 3)
                    var parent1 = SelectTournament(population, random, 3);
                    var parent2 = SelectTournament(population, random, 3);
                    
                    Route offspring;
                    if (random.NextDouble() < options.CrossoverRate)
                    {
                        // Crossover without calculating distance (will be calculated in parallel)
                        offspring = parent1.Crossover(parent2, skipDistanceCalculation: true);
                    }
                    else
                    {
                        // Copy without distance (will be calculated in parallel)
                        offspring = new Route(parent1, skipDistanceCalculation: true);
                    }
                    
                    if (random.NextDouble() < options.MutationRate)
                    {
                        // Mutate without calculating distance (will be calculated in parallel)
                        offspring.Mutate(skipDistanceCalculation: true);
                    }
                    
                    newPopulation.Add(offspring);
                }
                
                // Evaluate fitness in parallel (THIS IS WHERE REAL SPEEDUP HAPPENS!)
                // All routes in newPopulation (except elitism copy) have TotalDistance = 0
                // Workers calculate distances in parallel, master updates them
                await EvaluateFitnessParallel(moduleInfo, newPopulation, workerChannels);
                
                // Sort by fitness
                population = newPopulation.OrderBy(r => r.TotalDistance).ToList();
                
                bestDistance = population[0].TotalDistance;
                
                // Track convergence
                if (gen % 5 == 0 || gen == options.Generations - 1)
                {
                    convergenceHistory.Add(bestDistance);
                }
                
                if (gen % 10 == 0)
                {
                    moduleInfo.Logger.LogInformation("Generation {Gen}: Best={Best:F2}, Avg={Avg:F2}", 
                        gen, bestDistance, population.Average(r => r.TotalDistance));
                }
                
                // Early convergence check
                if (gen > 10 && IsConverged(convergenceHistory))
                {
                    moduleInfo.Logger.LogInformation("Converged at generation {Gen}", gen);
                    break;
                }
            }
            
            return new ModuleOutput
            {
                BestDistance = population[0].TotalDistance,
                AverageDistance = population.Average(r => r.TotalDistance),
                GenerationsCompleted = convergenceHistory.Count,
                BestRoute = population[0].Cities,
                ConvergenceHistory = convergenceHistory
            };
        }

        private static async Task EvaluateFitnessParallel(
            IModuleInfo moduleInfo,
            List<Route> routes,
            IChannel[] workerChannels)
        {
            // OPTIMIZATION: Use binary serialization instead of JSON for routes
            // Format: [numRoutes][route1Length][route1Data...][route2Length][route2Data...]...
            var routesPerWorker = routes.Count / workerChannels.Length;
            var tasks = new List<Task<List<double>>>();
            
            for (int i = 0; i < workerChannels.Length; i++)
            {
                int startIndex = i * routesPerWorker;
                int endIndex = (i == workerChannels.Length - 1) 
                    ? routes.Count 
                    : (i + 1) * routesPerWorker;
                
                int workerIndex = i;
                int batchSize = endIndex - startIndex;
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Send routes as binary data (much faster than JSON)
                        await WriteRoutesBinaryAsync(workerChannels[workerIndex], routes, startIndex, batchSize);
                        
                        // Receive fitness values as binary data
                        var fitnessValues = await ReadFitnessValuesBinaryAsync(workerChannels[workerIndex], batchSize);
                        return fitnessValues;
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogError(ex, "Error in parallel fitness evaluation for worker {WorkerIndex}", workerIndex);
                        return Enumerable.Repeat(double.MaxValue, batchSize).ToList();
                    }
                }));
            }
            
            var allFitnessValues = await Task.WhenAll(tasks);
            
            // Update route distances
            int routeIndex = 0;
            foreach (var fitnessBatch in allFitnessValues)
            {
                foreach (var fitness in fitnessBatch)
                {
                    routes[routeIndex].SetDistance(fitness);
                    routeIndex++;
                }
            }
        }

        /// <summary>
        /// Write routes as binary data for faster serialization.
        /// Format: [numRoutes (int)][route1Length (int)][route1Data (int[])][route2Length (int)][route2Data (int[])]...
        /// </summary>
        private static async ValueTask WriteRoutesBinaryAsync(IChannel channel, List<Route> routes, int startIndex, int count)
        {
            using var ms = new System.IO.MemoryStream();
            var writer = new System.IO.BinaryWriter(ms);
            
            writer.Write(count);
            
            for (int i = startIndex; i < startIndex + count; i++)
            {
                var routeCities = routes[i].Cities;
                writer.Write(routeCities.Count);
                foreach (var city in routeCities)
                {
                    writer.Write(city);
                }
            }
            
            await channel.WriteDataAsync(ms.ToArray());
        }

        /// <summary>
        /// Read fitness values as binary data for faster deserialization.
        /// Format: [numValues (int)][value1 (double)][value2 (double)]...
        /// </summary>
        private static async Task<List<double>> ReadFitnessValuesBinaryAsync(IChannel channel, int expectedCount)
        {
            var bytes = await channel.ReadBytesAsync();
            using var ms = new System.IO.MemoryStream(bytes);
            var reader = new System.IO.BinaryReader(ms);
            
            int count = reader.ReadInt32();
            var fitnessValues = new List<double>(count);
            
            for (int i = 0; i < count; i++)
            {
                fitnessValues.Add(reader.ReadDouble());
            }
            
            return fitnessValues;
        }

        /// <summary>
        /// Write cities as binary data for faster serialization.
        /// Format: [numCities (int)][city1Id (int)][city1X (double)][city1Y (double)]...
        /// </summary>
        private static async ValueTask WriteCitiesBinaryAsync(IChannel channel, List<City> cities)
        {
            using var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            writer.Write(cities.Count);
            foreach (var city in cities)
            {
                writer.Write(city.Id);
                writer.Write(city.X);
                writer.Write(city.Y);
            }
            
            await channel.WriteDataAsync(ms.ToArray());
        }

        private Route SelectTournament(List<Route> population, Random random, int tournamentSize)
        {
            var tournament = new List<Route>();
            for (int i = 0; i < tournamentSize; i++)
            {
                var randomIndex = random.Next(population.Count);
                tournament.Add(population[randomIndex]);
            }
            return tournament.OrderBy(r => r.TotalDistance).First();
        }

        private bool IsConverged(List<double> convergenceHistory)
        {
            if (convergenceHistory.Count < 10) return false;
            
            var recent = convergenceHistory.TakeLast(10).ToList();
            var improvement = recent.First() - recent.Last();
            var threshold = recent.First() * 0.001; // 0.1% improvement threshold
            
            return improvement < threshold;
        }

        private static async Task<List<City>> LoadOrGenerateCitiesAsync(IModuleInfo moduleInfo, ModuleOptions options)
        {
            if (options.LoadFromFile && !string.IsNullOrEmpty(options.InputFile))
            {
                try
                {
                    moduleInfo.Logger.LogInformation("Loading cities from file: {InputFile}", options.InputFile);
                    
                    if (options.InputFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(options.InputFile);
                        var jsonContent = await new StreamReader(fileStream).ReadToEndAsync();
                        var cities = JsonSerializer.Deserialize<List<City>>(jsonContent);
                        if (cities != null && cities.Count > 0)
                            return cities;
                    }
                    else
                    {
                        using var fileStream = moduleInfo.InputReader.GetFileStreamForFile(options.InputFile);
                        return CityLoader.LoadFromTextFile(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    moduleInfo.Logger.LogWarning(ex, "Error loading from file: {Message}", ex.Message);
                }
            }
            
            moduleInfo.Logger.LogInformation("Generating {CitiesNumber} random cities with seed={Seed}",
                options.CitiesNumber, options.Seed);
            return CityLoader.GenerateTestCities(options.CitiesNumber, options.Seed, TestCityPattern.Random);
        }

        private static async Task SaveResultsAsync(
            IModuleInfo   moduleInfo,
            List<City>    cities,
            ModuleOutput  result,
            ModuleOptions options)
        {
            try
            {
                // --- best_route.txt ---
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== TSP Best Route — Master-Slave (Parallel Fitness Evaluation) ===");
                sb.AppendLine();
                sb.AppendLine($"Best distance    : {result.BestDistance:F2}");
                sb.AppendLine($"Average distance : {result.AverageDistance:F2}");
                sb.AppendLine($"Cities           : {result.BestRoute.Count}");
                sb.AppendLine($"Generations      : {result.GenerationsCompleted}");
                sb.AppendLine();
                sb.AppendLine("--- Timing breakdown ---");
                sb.AppendLine($"Pod provisioning : {result.PodProvisioningSeconds:F2} s  (KEDA scheduling + pod startup)");
                sb.AppendLine($"Computation      : {result.ComputeSeconds:F2} s  (actual GA work)");
                sb.AppendLine($"Total elapsed    : {result.ElapsedSeconds:F2} s");
                sb.AppendLine();
                sb.AppendLine("--- Best route ---");
                sb.AppendLine(string.Join(" → ", result.BestRoute) + " → " + result.BestRoute[0]);

                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                    options.BestRouteFile);

                // --- best_route.svg ---
                var routeSvg = SvgGenerator.GenerateRouteSvg(cities, result.BestRoute);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(routeSvg),
                    "best_route.svg");

                // --- convergence.svg ---
                if (result.ConvergenceHistory.Count >= 2)
                {
                    var convergenceSvg = SvgGenerator.GenerateConvergenceSvg(result.ConvergenceHistory);
                    await moduleInfo.OutputWriter.WriteToFileAsync(
                        System.Text.Encoding.UTF8.GetBytes(convergenceSvg),
                        "convergence.svg");
                }

                moduleInfo.Logger.LogInformation(
                    "Results saved: {BestRouteFile}, best_route.svg, convergence.svg",
                    options.BestRouteFile);
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Error saving results: {Message}", ex.Message);
            }
        }
    }
}

