using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Master module for Island Model with Migration.
    ///
    /// Protocol:
    ///   1. Create all worker points in one batch (all Service Bus messages published at once).
    ///   2. Launch all worker modules via ExecuteClassAsync (MUST come before any data writes).
    ///   3. Send cities + options to every worker (workers are now running and reading).
    ///   4. For each migration round, coordinate ring-topology migration:
    ///        a. Read migrants from all workers.
    ///        b. Forward: worker i's migrants go to worker (i+1) mod N.
    ///   5. Collect final ModuleOutput from every worker.
    ///   6. Pick the best result and write output.
    /// </summary>
    public class IslandModelWithMigrationMainModule : IModule
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = moduleInfo.BindModuleOptions<ModuleOptions>();
                var cities  = await LoadOrGenerateCitiesAsync(moduleInfo, options);

                options.CitiesNumber = cities.Count;

                moduleInfo.Logger.LogInformation(
                    "Island Model with migration: {Cities} cities, {Points} islands, {Gen} generations, interval={Interval}",
                    cities.Count, options.PointsNumber, options.Generations, options.MigrationInterval);

                var stopwatch = Stopwatch.StartNew();

                // --- Step 1: Create all points as a single batch (pod provisioning phase) ---
                // All Service Bus messages are published before any connection is awaited,
                // so KEDA sees the full queue depth immediately and AKS CA can provision
                // all required nodes in parallel (article Fig. 2).
                var points   = await moduleInfo.CreatePointsAsync(options.PointsNumber);
                var channels = new IChannel[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    channels[i] = await points[i].CreateChannelAsync();
                }
                // All daemons are connected — record infrastructure overhead.
                var podProvisioningSeconds = stopwatch.Elapsed.TotalSeconds;

                moduleInfo.Logger.LogInformation("Created {Count} islands", points.Length);

                // --- Step 2: Launch worker modules ---
                // ExecuteClass MUST be sent before any data is written to the channel.
                // The daemon's ChannelOrchestrator is in its signal-reading loop after
                // InitializeJob completes; writing data bytes before ExecuteClass causes
                // the orchestrator to interpret the length prefix as a Signal enum value,
                // corrupting the protocol.  Only after the daemon has dispatched
                // ExecuteClassSignalHandler and the worker module's RunAsync is running
                // will the channel reads (cities, options) be consumed correctly.
                foreach (var point in points)
                {
                    await point.ExecuteClassAsync<IslandModelWithMigrationWorkerModule>();
                }

                moduleInfo.Logger.LogInformation("All worker modules launched");

                // --- Step 3: Send data to all workers ---
                // Worker modules are now running and blocking on ReadObjectAsync — safe to write.
                for (int i = 0; i < points.Length; i++)
                {
                    await channels[i].WriteObjectAsync(cities);
                    await channels[i].WriteObjectAsync(options);
                }

                // --- Step 4: Coordinate migration rounds ---
                // The number of rounds is deterministic and the same for master and every worker,
                // so they stay in lockstep without any extra signalling.
                int numMigrationRounds = options.EnableMigration && options.MigrationInterval > 0
                    ? options.Generations / options.MigrationInterval
                    : 0;

                for (int round = 0; round < numMigrationRounds; round++)
                {
                    moduleInfo.Logger.LogInformation(
                        "Migration round {Round}/{Total}", round + 1, numMigrationRounds);

                    await PerformMigrationRoundAsync(moduleInfo, channels, round + 1, numMigrationRounds);
                }

                // --- Step 5: Collect final results (graceful: skip islands that failed) ---
                var finalResults = new List<ModuleOutput>();
                for (int i = 0; i < channels.Length; i++)
                {
                    try
                    {
                        var result = await channels[i].ReadObjectAsync<ModuleOutput>();
                        finalResults.Add(result);
                        moduleInfo.Logger.LogInformation(
                            "Island {Index} finished — best distance: {Best:F2}", i, result.BestDistance);
                    }
                    catch (Exception ex)
                    {
                        moduleInfo.Logger.LogWarning(ex,
                            "Island {Index} failed to return a result, skipping: {Message}", i, ex.Message);
                    }
                }

                if (finalResults.Count == 0)
                    throw new InvalidOperationException("All islands failed to return results");

                if (finalResults.Count < channels.Length)
                    moduleInfo.Logger.LogWarning(
                        "Only {Received}/{Total} islands returned results; proceeding with partial results",
                        finalResults.Count, channels.Length);

                stopwatch.Stop();

                // --- Step 6: Combine and write output ---
                var bestResult = finalResults.OrderBy(r => r.BestDistance).First();
                var combined   = new ModuleOutput
                {
                    BestDistance          = bestResult.BestDistance,
                    AverageDistance       = finalResults.Average(r => r.AverageDistance),
                    GenerationsCompleted  = finalResults.Max(r => r.GenerationsCompleted),
                    BestRoute             = bestResult.BestRoute,
                    ConvergenceHistory    = CombineConvergenceHistories(finalResults),
                    ElapsedSeconds        = stopwatch.Elapsed.TotalSeconds,
                    PodProvisioningSeconds = podProvisioningSeconds,
                    ComputeSeconds        = stopwatch.Elapsed.TotalSeconds - podProvisioningSeconds
                };

                if (options.SaveResults)
                {
                    await SaveResultsAsync(moduleInfo, cities, combined, options);
                }

                var jsonContent = JsonSerializer.Serialize(combined, JsonSerializerOptions);
                await moduleInfo.OutputWriter.WriteToFileAsync(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent), options.OutputFile);

                moduleInfo.Logger.LogInformation(
                    "Island Model with migration completed in {Elapsed:F2}s (pod provisioning: {Prov:F2}s, compute: {Comp:F2}s) — best distance: {Best:F2}",
                    combined.ElapsedSeconds, combined.PodProvisioningSeconds, combined.ComputeSeconds, combined.BestDistance);

                // Cleanup
                foreach (var point in points)
                {
                    try { await point.DeleteAsync(); } catch { /* best-effort */ }
                }
                foreach (var channel in channels)
                {
                    try { channel.Dispose(); } catch { /* best-effort */ }
                }
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Critical error in Island Model with migration: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Coordinates one migration round: reads migrants from every worker then forwards them
        /// in a ring topology (worker i sends its migrants to worker (i+1) mod N).
        /// <para>
        /// Workers serialize migrants as <see cref="MigrantDto"/> (not <see cref="Route"/>) because
        /// <c>System.Text.Json</c> cannot deserialize <see cref="Route"/> — it has no parameterless
        /// constructor. The master simply relays the opaque DTO list; reconstruction into full
        /// <see cref="Route"/> objects happens on the receiving worker.
        /// </para>
        /// </summary>
        private static async Task PerformMigrationRoundAsync(
            IModuleInfo moduleInfo,
            IChannel[] channels,
            int roundNumber,
            int totalRounds)
        {
            // Read migrant DTOs from all workers first.
            var allMigrants = new List<MigrantDto>[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                allMigrants[i] = await channels[i].ReadObjectAsync<List<MigrantDto>>();
                moduleInfo.Logger.LogInformation(
                    "Round {Round}/{Total}: island {Index} sent {Count} migrants",
                    roundNumber, totalRounds, i, allMigrants[i]?.Count ?? 0);
            }

            // Forward migrants in ring topology.
            for (int i = 0; i < channels.Length; i++)
            {
                int targetIndex    = (i + 1) % channels.Length;
                var migrantsToSend = allMigrants[i] ?? new List<MigrantDto>();
                await channels[targetIndex].WriteObjectAsync(migrantsToSend);

                moduleInfo.Logger.LogInformation(
                    "Round {Round}/{Total}: forwarded {Count} migrants: island {From} → island {To}",
                    roundNumber, totalRounds, migrantsToSend.Count, i, targetIndex);
            }
        }

        private static List<double> CombineConvergenceHistories(List<ModuleOutput> results)
        {
            if (results.Count == 0) return new List<double>();

            int maxLength = results.Max(r => r.ConvergenceHistory?.Count ?? 0);
            var combined  = new List<double>(maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                var values = results
                    .Where(r => r.ConvergenceHistory != null && i < r.ConvergenceHistory.Count)
                    .Select(r => r.ConvergenceHistory[i])
                    .ToList();

                if (values.Count > 0)
                    combined.Add(values.Min()); // best across all islands at this generation
            }

            return combined;
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
                    moduleInfo.Logger.LogWarning(ex, "Failed to load from file: {Message}", ex.Message);
                }
            }

            moduleInfo.Logger.LogInformation(
                "Generating {Count} random cities with seed={Seed}",
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
                sb.AppendLine("=== TSP Best Route — Island Model with Migration ===");
                sb.AppendLine();
                sb.AppendLine($"Best distance    : {result.BestDistance:F2}");
                sb.AppendLine($"Average distance : {result.AverageDistance:F2}");
                sb.AppendLine($"Cities           : {result.BestRoute.Count}");
                sb.AppendLine($"Generations      : {result.GenerationsCompleted}");
                sb.AppendLine();
                sb.AppendLine("--- Timing breakdown ---");
                sb.AppendLine($"Pod provisioning : {result.PodProvisioningSeconds:F2} s  (KEDA scheduling + pod startup)");
                sb.AppendLine($"Computation      : {result.ComputeSeconds:F2} s  (actual GA work incl. migration rounds)");
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
