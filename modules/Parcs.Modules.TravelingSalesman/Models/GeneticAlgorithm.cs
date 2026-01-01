namespace Parcs.Modules.TravelingSalesman.Models
{
    public class GeneticAlgorithm
    {
        private List<Route> _population;
        private readonly List<City> _cities;
        private readonly ModuleOptions _options;
        private readonly Random _random;
        private readonly List<double> _convergenceHistory = new();
        private readonly MigrationManager? _migrationManager;
        private readonly bool _enableMigration;

        public GeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            _cities = cities ?? throw new ArgumentNullException(nameof(cities));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _random = new Random(options.Seed);
            _convergenceHistory = new List<double>();
            _population = new List<Route>(); // Ініціалізуємо популяцію
        }

        public void Initialize()
        {
            _population.Clear();
            
            for (int i = 0; i < _options.PopulationSize; i++)
            {
                var route = new Route(_cities, _random);
                _population.Add(route);
            }
        }

        public void Evolve()
        {
            if (_population == null || _population.Count == 0)
            {
                throw new InvalidOperationException("Population must be initialized before evolving. Call Initialize() first.");
            }
            
            var newPopulation = new List<Route>();
            
            // Elitism: keep the best individual to ensure no regression
            var bestRoute = GetBestRoute();
            newPopulation.Add(bestRoute);
            
            while (newPopulation.Count < _options.PopulationSize)
            {
                var parent1 = Select();
                var parent2 = Select();
                
                Route offspring;
                if (_random.NextDouble() < _options.CrossoverRate)
                {
                    offspring = parent1.Crossover(parent2);
                }
                else
                {
                    offspring = new Route(parent1);
                }
                
                if (_random.NextDouble() < _options.MutationRate)
                {
                    offspring.Mutate();
                }
                
                newPopulation.Add(offspring);
            }
            
            _population = newPopulation;
        }

        private Route Select()
        {
            const int tournamentSize = 3;
            var tournament = new List<Route>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                var randomIndex = _random.Next(_population.Count);
                tournament.Add(_population[randomIndex]);
            }
            
            return tournament.OrderBy(r => r.TotalDistance).First();
        }

        public Route GetBestRoute()
        {
            return _population.OrderBy(r => r.TotalDistance).First();
        }

        public double GetAverageDistance()
        {
            return _population.Average(r => r.TotalDistance);
        }

        public void RunGenerations(int generations)
        {
            for (int gen = 0; gen < generations; gen++)
            {
                Evolve();
                
                var bestDistance = GetBestRoute().TotalDistance;
                // Записуємо історію збіжності
                if (gen % 5 == 0 || gen == generations - 1)
                {
                    _convergenceHistory.Add(bestDistance);
                }
                
                // Обробляємо міграцію якщо потрібно
                if (_enableMigration && _migrationManager != null && _migrationManager.ShouldMigrate(gen))
                {
                    ProcessMigration();
                }
                
                if (gen > 10 && IsConverged())
                {
                    break;
                }
            }
        }

        private bool IsConverged()
        {
            if (_convergenceHistory.Count < 10) return false;
            
            var recent = _convergenceHistory.TakeLast(10).ToList();
            var improvement = recent.First() - recent.Last();
            var threshold = recent.First() * 0.001; // 0.1% improvement threshold
            
            return improvement < threshold;
        }

        public List<double> GetConvergenceHistory()
        {
            return new List<double>(_convergenceHistory);
        }

        public void Optimize()
        {
            Initialize();
            RunGenerations(_options.Generations);
        }

        public Route GetOptimizedRoute()
        {
            Optimize();
            return GetBestRoute();
        }

        /// <summary>
        /// Обробляє міграцію особин
        /// </summary>
        private void ProcessMigration()
        {
            if (_migrationManager == null || _population == null || _population.Count == 0) return;
            
            try
            {
                // Вибір особин для міграції
                var migrants = _migrationManager.SelectIndividualsForMigration(_population);
                if (migrants.Count == 0) return;
                
                // Додаємо їх до черги міграції
                foreach (var migrant in migrants)
                {
                    if (migrant != null)
                    {
                        _migrationManager.AddToMigration(migrant);
                    }
                }
                
                // Отримуємо мігрантів від інших воркерів
                var incomingMigrants = new List<Route>();
                while (_migrationManager.TryGetFromMigration(out var incomingMigrant))
                {
                    if (incomingMigrant != null)
                    {
                        incomingMigrants.Add(incomingMigrant);
                    }
                }
                
                // Виконуємо міграцію
                if (incomingMigrants.Count > 0)
                {
                    _migrationManager.PerformMigration(_population, incomingMigrants);
                }
            }
            catch (Exception ex)
            {
                // Логуємо помилку, але не зупиняємо роботу
                System.Diagnostics.Debug.WriteLine($"Migration processing error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Отримує міграційного менеджера
        /// </summary>
        public MigrationManager? GetMigrationManager()
        {
            return _migrationManager;
        }
        
        /// <summary>
        /// Додає особу до міграції
        /// </summary>
        public void AddToMigration(Route route)
        {
            _migrationManager?.AddToMigration(route);
        }
        
        /// <summary>
        /// Отримує особу з міграції
        /// </summary>
        public bool TryGetFromMigration(out Route? route)
        {
            if (_migrationManager != null)
            {
                return _migrationManager.TryGetFromMigration(out route);
            }
            
            route = null;
            return false;
        }

        /// <summary>
        /// Отримує поточну популяцію
        /// </summary>
        public List<Route>? GetPopulation()
        {
            return _population;
        }
    }
} 