using System.Collections.Concurrent;

namespace Parcs.Modules.TravelingSalesman.Models
{
    /// <summary>
    /// Migration manager for parallel genetic algorithm.
    /// </summary>
    public class MigrationManager
    {
        private readonly ConcurrentQueue<Route> _migrationQueue = new();
        private readonly object _lockObject = new();
        private readonly Random _random = new();
        
        /// <summary>
        /// Number of individuals for migration.
        /// </summary>
        public int MigrationSize { get; set; } = 5;

        /// <summary>
        /// Migration interval (every N generations).
        /// </summary>
        public int MigrationInterval { get; set; } = 10;

        /// <summary>
        /// Migration type.
        /// </summary>
        public MigrationType MigrationType { get; set; } = MigrationType.BestIndividuals;

        /// <summary>
        /// Adds an individual to the migration queue.
        /// </summary>
        public void AddToMigration(Route route)
        {
            _migrationQueue.Enqueue(route);
        }
        
        /// <summary>
        /// Gets an individual from the migration queue.
        /// </summary>
        public bool TryGetFromMigration(out Route? route)
        {
            return _migrationQueue.TryDequeue(out route);
        }
        
        /// <summary>
        /// Clears the migration queue.
        /// </summary>
        public void ClearMigrationQueue()
        {
            while (_migrationQueue.TryDequeue(out _)) { }
        }
        
        /// <summary>
        /// Performs migration between populations.
        /// </summary>
        public void PerformMigration(List<Route> population, List<Route> migrants)
        {
            if (population == null || migrants == null || migrants.Count == 0) return;
            
            lock (_lockObject)
            {
                try
                {
                    // Remove worst individuals
                    var worstCount = Math.Min(migrants.Count, Math.Max(1, population.Count / 10));
                    if (worstCount > 0 && population.Count > worstCount)
                    {
                        population.Sort((a, b) => a.TotalDistance.CompareTo(b.TotalDistance));
                        population.RemoveRange(population.Count - worstCount, worstCount);
                    }
                    
                    // Add migrants
                    population.AddRange(migrants);

                    // Preserve population size if it exceeds initial capacity
                    var initialCapacity = population.Capacity;
                    if (population.Count > initialCapacity)
                    {
                        population.RemoveRange(initialCapacity, population.Count - initialCapacity);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't stop execution
                    System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Selects individuals for migration.
        /// </summary>
        public List<Route> SelectIndividualsForMigration(List<Route> population)
        {
            if (population == null || population.Count == 0)
            {
                return new List<Route>();
            }
            
            var migrants = new List<Route>();
            var actualMigrationSize = Math.Min(MigrationSize, population.Count);
            
            switch (MigrationType)
            {
                case MigrationType.BestIndividuals:
                    // Best individuals
                    migrants.AddRange(population
                        .OrderBy(r => r.TotalDistance)
                        .Take(actualMigrationSize));
                    break;
                    
                case MigrationType.RandomIndividuals:
                    // Random individuals
                    var shuffled = population.OrderBy(x => _random.Next()).ToList();
                    migrants.AddRange(shuffled.Take(actualMigrationSize));
                    break;
                    
                case MigrationType.DiverseIndividuals:
                    // Diverse individuals (varying distances)
                    var sorted = population.OrderBy(r => r.TotalDistance).ToList();
                    var step = Math.Max(1, sorted.Count / actualMigrationSize);
                    for (int i = 0; i < actualMigrationSize && i * step < sorted.Count; i++)
                    {
                        migrants.Add(sorted[i * step]);
                    }
                    break;
                    
                case MigrationType.TournamentSelection:
                    // Tournament selection
                    for (int i = 0; i < actualMigrationSize; i++)
                    {
                        var tournament = population
                            .OrderBy(x => _random.Next())
                            .Take(Math.Min(3, population.Count))
                            .OrderBy(r => r.TotalDistance)
                            .First();
                        migrants.Add(tournament);
                    }
                    break;
            }
            
            return migrants;
        }
        
        /// <summary>
        /// Checks whether migration is needed.
        /// </summary>
        public bool ShouldMigrate(int generation)
        {
            return generation > 0 && generation % MigrationInterval == 0;
        }
    }
    
    /// <summary>
    /// Migration types.
    /// </summary>
    public enum MigrationType
    {
        /// <summary>
        /// Best individuals.
        /// </summary>
        BestIndividuals,

        /// <summary>
        /// Random individuals.
        /// </summary>
        RandomIndividuals,

        /// <summary>
        /// Diverse individuals.
        /// </summary>
        DiverseIndividuals,

        /// <summary>
        /// Tournament selection.
        /// </summary>
        TournamentSelection
    }
} 