using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Worker module for Island Model with Migration.
    /// Runs local GA evolution and participates in periodic migrations with other islands.
    /// </summary>
    public class IslandModelWithMigrationWorkerModule : IModule
    {
        private List<City>? _cities;
        private ModuleOptions? _options;
        private GeneticAlgorithm? _geneticAlgorithm;
        private MigrationManager? _migrationManager;
        private int _currentGeneration = 0;

        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("Island Model Worker з міграцією запущено");

            try
            {
                // Receive cities and options
                _cities = await moduleInfo.Parent.ReadObjectAsync<List<City>>();
                _options = await moduleInfo.Parent.ReadObjectAsync<ModuleOptions>();

                if (_cities == null || _options == null)
                {
                    throw new InvalidOperationException("Не вдалося отримати дані міст або опції");
                }

                moduleInfo.Logger.LogInformation("Worker отримав {CitiesCount} міст", _cities.Count);

                // Configure local options for this island
                var localOptions = new ModuleOptions
                {
                    CitiesNumber = _options.CitiesNumber,
                    PopulationSize = _options.PopulationSize, // Each island uses full population
                    Generations = _options.Generations,
                    MutationRate = _options.MutationRate,
                    CrossoverRate = _options.CrossoverRate,
                    Seed = _options.Seed + Environment.CurrentManagedThreadId, // Different seed per worker
                    EnableMigration = _options.EnableMigration,
                    MigrationType = _options.MigrationType,
                    MigrationSize = _options.MigrationSize,
                    MigrationInterval = _options.MigrationInterval
                };

                // Initialize genetic algorithm with migration support
                _geneticAlgorithm = new GeneticAlgorithm(_cities, localOptions);
                
                if (_options.EnableMigration)
                {
                    _migrationManager = new MigrationManager
                    {
                        MigrationSize = _options.MigrationSize,
                        MigrationInterval = _options.MigrationInterval,
                        MigrationType = _options.MigrationType
                    };
                    _geneticAlgorithm.EnableMigration(_migrationManager);
                }

                _geneticAlgorithm.Initialize();

                moduleInfo.Logger.LogInformation("Worker ініціалізовано. Population={Population}", localOptions.PopulationSize);

                // Main evolution loop - run all generations with periodic migrations
                for (int gen = 0; gen < _options.Generations; gen++)
                {
                    _currentGeneration = gen;
                    
                    // Check if migration should happen
                    if (_options.EnableMigration && 
                        _migrationManager != null &&
                        _migrationManager.ShouldMigrate(gen))
                    {
                        moduleInfo.Logger.LogInformation("Worker: Покоління {Gen} - виконуємо міграцію", gen);
                        
                        // Request migration coordination from master
                        await moduleInfo.Parent.WriteSignalAsync(Signal.ExecuteClass);
                        await HandleMigrationPhaseAsync(moduleInfo);
                    }
                    
                    // Evolve one generation
                    _geneticAlgorithm.Evolve();
                    
                    // Log progress occasionally
                    if (gen % 20 == 0 || gen == _options.Generations - 1)
                    {
                        var bestRoute = _geneticAlgorithm.GetBestRoute();
                        moduleInfo.Logger.LogInformation(
                            "Worker: Покоління {Gen}, Найкраща відстань: {Best:F2}", 
                            gen, bestRoute.TotalDistance);
                    }
                }

                // Send final result
                moduleInfo.Logger.LogInformation("Worker: Еволюція завершена. Відправка результату...");
                var finalResult = GenerateResult();
                await moduleInfo.Parent.WriteObjectAsync(finalResult);

                moduleInfo.Logger.LogInformation("Worker завершено");
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogError(ex, "Помилка в worker модулі: {Message}", ex.Message);
                throw;
            }
        }

        private async Task HandleMigrationPhaseAsync(IModuleInfo moduleInfo)
        {
            if (_migrationManager == null || _geneticAlgorithm == null || _cities == null)
            {
                return;
            }

            var population = _geneticAlgorithm.GetPopulation();
            var migrantsToSend = _migrationManager.SelectIndividualsForMigration(population);
            
            await moduleInfo.Parent.WriteObjectAsync(migrantsToSend.Select(r => new Route(r)).ToArray());

            var incomingMigrants = await moduleInfo.Parent.ReadObjectAsync<List<Route>>();
            
            if (incomingMigrants != null && incomingMigrants.Count > 0)
            {
                foreach (var migrant in incomingMigrants)
                {
                    migrant.SetCities(_cities);
                }

                _migrationManager.PerformMigration(population, incomingMigrants);
            }

            await moduleInfo.Parent.WriteDataAsync(true);
        }

        private ModuleOutput GenerateResult()
        {
            if (_geneticAlgorithm == null || _cities == null)
                throw new InvalidOperationException("Genetic algorithm not initialized");

            var bestRoute = _geneticAlgorithm.GetBestRoute();
            var avgDistance = _geneticAlgorithm.GetAverageDistance();
            var convergenceHistory = _geneticAlgorithm.GetConvergenceHistory();

            return new ModuleOutput
            {
                BestDistance = bestRoute.TotalDistance,
                AverageDistance = avgDistance,
                GenerationsCompleted = _currentGeneration,
                BestRoute = bestRoute.Cities,
                ConvergenceHistory = convergenceHistory
            };
        }

        private class GenerationResult
        {
            public int Generation { get; set; }
            public double BestDistance { get; set; }
            public double AverageDistance { get; set; }
        }
    }
}

