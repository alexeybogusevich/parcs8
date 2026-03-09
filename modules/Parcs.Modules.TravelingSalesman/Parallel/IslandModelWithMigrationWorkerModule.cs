using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Worker module for Island Model with Migration.
    ///
    /// Protocol (master-driven, deterministic):
    ///   1. Receive cities and options from master.
    ///   2. Loop for numMigrationRounds = Generations / MigrationInterval rounds:
    ///        a. Run MigrationInterval generations.
    ///        b. Send best migrants to master (WriteObjectAsync).
    ///        c. Receive incoming migrants from master (ReadObjectAsync).
    ///        d. Apply migration to local population.
    ///   3. Run any remaining generations (Generations % MigrationInterval).
    ///   4. Send final ModuleOutput to master.
    ///
    /// The master coordinates all migration rounds in lockstep, so no extra signals are needed.
    /// </summary>
    public class IslandModelWithMigrationWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("Island Model Worker with migration started");

            try
            {
                var cities  = await moduleInfo.Parent.ReadObjectAsync<List<City>>();
                var options = await moduleInfo.Parent.ReadObjectAsync<ModuleOptions>();

                if (cities == null || options == null)
                    throw new InvalidOperationException("Failed to receive cities or options from master");

                moduleInfo.Logger.LogInformation(
                    "Worker received {CitiesCount} cities, Generations={Gen}, MigrationInterval={Interval}",
                    cities.Count, options.Generations, options.MigrationInterval);

                // Each island uses a different random seed so populations diverge naturally.
                var localOptions = new ModuleOptions
                {
                    CitiesNumber      = options.CitiesNumber,
                    PopulationSize    = options.PopulationSize,
                    Generations       = options.Generations,
                    MutationRate      = options.MutationRate,
                    CrossoverRate     = options.CrossoverRate,
                    Seed              = options.Seed + Environment.CurrentManagedThreadId,
                    EnableMigration   = options.EnableMigration,
                    MigrationType     = options.MigrationType,
                    MigrationSize     = options.MigrationSize,
                    MigrationInterval = options.MigrationInterval
                };

                var ga = new GeneticAlgorithm(cities, localOptions);
                ga.Initialize();

                var migrationManager = new MigrationManager
                {
                    MigrationSize     = options.MigrationSize,
                    MigrationInterval = options.MigrationInterval,
                    MigrationType     = options.MigrationType
                };

                int numMigrationRounds = options.EnableMigration && options.MigrationInterval > 0
                    ? options.Generations / options.MigrationInterval
                    : 0;
                int remainingGenerations = options.Generations - numMigrationRounds * options.MigrationInterval;

                moduleInfo.Logger.LogInformation(
                    "Worker will run {Rounds} migration rounds then {Remaining} remaining generations",
                    numMigrationRounds, remainingGenerations);

                // --- Main evolution loop with migration ---
                for (int round = 0; round < numMigrationRounds; round++)
                {
                    // Evolve for one interval independently.
                    ga.RunGenerations(options.MigrationInterval);

                    var population = ga.GetPopulation();
                    var migrants   = migrationManager.SelectIndividualsForMigration(population);

                    moduleInfo.Logger.LogInformation(
                        "Worker: round {Round}/{Total} — sending {Count} migrants to master",
                        round + 1, numMigrationRounds, migrants.Count);

                    // Send our selected individuals to the master for ring-topology redistribution.
                    await moduleInfo.Parent.WriteObjectAsync(migrants);

                    // Receive migrants from the neighbouring island (forwarded by master).
                    var incomingMigrants = await moduleInfo.Parent.ReadObjectAsync<List<Route>>();

                    if (incomingMigrants != null && incomingMigrants.Count > 0)
                    {
                        // Restore city references that are lost during JSON serialization.
                        foreach (var migrant in incomingMigrants)
                        {
                            migrant.SetCities(cities);
                        }

                        migrationManager.PerformMigration(population, incomingMigrants);

                        moduleInfo.Logger.LogInformation(
                            "Worker: round {Round} — integrated {Count} incoming migrants",
                            round + 1, incomingMigrants.Count);
                    }
                }

                // Run any leftover generations after the last full interval.
                if (remainingGenerations > 0)
                {
                    ga.RunGenerations(remainingGenerations);
                }

                // --- Send final result ---
                var bestRoute   = ga.GetBestRoute();
                var avgDistance = ga.GetAverageDistance();
                var convergence = ga.GetConvergenceHistory();

                var result = new ModuleOutput
                {
                    BestDistance         = bestRoute.TotalDistance,
                    AverageDistance      = avgDistance,
                    GenerationsCompleted = options.Generations,
                    BestRoute            = bestRoute.Cities,
                    ConvergenceHistory   = convergence
                };

                await moduleInfo.Parent.WriteObjectAsync(result);

                moduleInfo.Logger.LogInformation("Worker finished — best distance: {Best:F2}", result.BestDistance);
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Error in migration worker: {Message}", ex.Message);
                throw;
            }
        }
    }
}
