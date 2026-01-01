using System.Collections.Concurrent;

namespace Parcs.Modules.TravelingSalesman.Models
{
    /// <summary>
    /// Менеджер міграції для паралельного генетичного алгоритму
    /// </summary>
    public class MigrationManager
    {
        private readonly ConcurrentQueue<Route> _migrationQueue = new();
        private readonly object _lockObject = new();
        private readonly Random _random = new();
        
        /// <summary>
        /// Кількість особей для міграції
        /// </summary>
        public int MigrationSize { get; set; } = 5;
        
        /// <summary>
        /// Інтервал міграції (кожні N поколінь)
        /// </summary>
        public int MigrationInterval { get; set; } = 10;
        
        /// <summary>
        /// Тип міграції
        /// </summary>
        public MigrationType MigrationType { get; set; } = MigrationType.BestIndividuals;
        
        /// <summary>
        /// Додає особу до черги міграції
        /// </summary>
        public void AddToMigration(Route route)
        {
            _migrationQueue.Enqueue(route);
        }
        
        /// <summary>
        /// Отримує особу з черги міграції
        /// </summary>
        public bool TryGetFromMigration(out Route? route)
        {
            return _migrationQueue.TryDequeue(out route);
        }
        
        /// <summary>
        /// Очищає чергу міграції
        /// </summary>
        public void ClearMigrationQueue()
        {
            while (_migrationQueue.TryDequeue(out _)) { }
        }
        
        /// <summary>
        /// Виконує міграцію між популяціями
        /// </summary>
        public void PerformMigration(List<Route> population, List<Route> migrants)
        {
            if (population == null || migrants == null || migrants.Count == 0) return;
            
            lock (_lockObject)
            {
                try
                {
                    // Видаляємо найгірші особини
                    var worstCount = Math.Min(migrants.Count, Math.Max(1, population.Count / 10));
                    if (worstCount > 0 && population.Count > worstCount)
                    {
                        population.Sort((a, b) => a.TotalDistance.CompareTo(b.TotalDistance));
                        population.RemoveRange(population.Count - worstCount, worstCount);
                    }
                    
                    // Додаємо мігрантів
                    population.AddRange(migrants);
                    
                    // Зберігаємо розмір популяції якщо він перевищує початкову ємність
                    var initialCapacity = population.Capacity;
                    if (population.Count > initialCapacity)
                    {
                        population.RemoveRange(initialCapacity, population.Count - initialCapacity);
                    }
                }
                catch (Exception ex)
                {
                    // Логуємо помилку, але не зупиняємо роботу
                    System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Вибір особин для міграції
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
                    // Кращі особини
                    migrants.AddRange(population
                        .OrderBy(r => r.TotalDistance)
                        .Take(actualMigrationSize));
                    break;
                    
                case MigrationType.RandomIndividuals:
                    // Випадкові особини
                    var shuffled = population.OrderBy(x => _random.Next()).ToList();
                    migrants.AddRange(shuffled.Take(actualMigrationSize));
                    break;
                    
                case MigrationType.DiverseIndividuals:
                    // Різноманітні особини (різні відстані)
                    var sorted = population.OrderBy(r => r.TotalDistance).ToList();
                    var step = Math.Max(1, sorted.Count / actualMigrationSize);
                    for (int i = 0; i < actualMigrationSize && i * step < sorted.Count; i++)
                    {
                        migrants.Add(sorted[i * step]);
                    }
                    break;
                    
                case MigrationType.TournamentSelection:
                    // Турнірний вибір
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
        /// Перевіряє, чи потрібна міграція
        /// </summary>
        public bool ShouldMigrate(int generation)
        {
            return generation > 0 && generation % MigrationInterval == 0;
        }
    }
    
    /// <summary>
    /// Типи міграції
    /// </summary>
    public enum MigrationType
    {
        /// <summary>
        /// Кращі особини
        /// </summary>
        BestIndividuals,
        
        /// <summary>
        /// Випадкові особини
        /// </summary>
        RandomIndividuals,
        
        /// <summary>
        /// Різноманітні особини
        /// </summary>
        DiverseIndividuals,
        
        /// <summary>
        /// Турнірний вибір
        /// </summary>
        TournamentSelection
    }
} 