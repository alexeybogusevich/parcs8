using System;
using System.Collections.Generic;
using System.Linq;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public class GeneticAlgorithm
    {
        private List<Route> _population;
        private readonly List<City> _cities;
        private readonly ModuleOptions _options;
        private readonly Random _random;
        private readonly List<double> _convergenceHistory;

        public GeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            _cities = cities ?? throw new ArgumentNullException(nameof(cities));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _random = new Random(_options.Seed);
            _convergenceHistory = new List<double>();
            _population = new List<Route>();
        }

        public void Initialize()
        {
            _population.Clear();
            
            // Create initial population with random routes
            for (int i = 0; i < _options.PopulationSize; i++)
            {
                var route = new Route(_cities, _random);
                _population.Add(route);
            }
        }

        public void Evolve()
        {
            var newPopulation = new List<Route>();
            
            // Elitism: keep the best individual
            var bestRoute = GetBestRoute();
            newPopulation.Add(bestRoute);
            
            // Generate new individuals through selection, crossover, and mutation
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
            // Tournament selection
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
                
                // Record convergence history
                var bestDistance = GetBestRoute().TotalDistance;
                var averageDistance = GetAverageDistance();
                _convergenceHistory.Add(bestDistance);
                
                // Optional: early stopping if convergence is reached
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
    }
} 