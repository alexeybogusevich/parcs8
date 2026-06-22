using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public IModuleInfo GetModuleInfo()
        {
            return new ModuleInfo
            {
                Name = "Parallel TSP Worker Module",
                Description = "Worker module for parallel TSP genetic algorithm execution"
            };
        }

        public void Run(IModuleInfo moduleInfo, IChannel channel)
        {
            Console.WriteLine("Worker module started");
            
            // Read input data from main module
            var cities = channel.ReadObject<List<City>>();
            var options = channel.ReadObject<ModuleOptions>();
            
            Console.WriteLine($"Worker received {cities.Count} cities");
            Console.WriteLine($"Population: {options.PopulationSize}, Generations: {options.Generations}");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Run local genetic algorithm
            var result = RunLocalGeneticAlgorithm(cities, options);
            
            stopwatch.Stop();
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            
            Console.WriteLine($"Worker completed in {result.ElapsedSeconds:F2} seconds");
            Console.WriteLine($"Local best distance: {result.BestDistance:F2}");
            Console.WriteLine($"Local average distance: {result.AverageDistance:F2}");
            
            // Send result back to main module
            channel.WriteObject(result);
        }

        private ModuleOutput RunLocalGeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            // Create a local copy of options with adjusted population size
            var localOptions = new ModuleOptions
            {
                CitiesNumber = options.CitiesNumber,
                PopulationSize = options.PopulationSize / options.PointsNumber, // Distribute population
                Generations = options.Generations,
                MutationRate = options.MutationRate,
                CrossoverRate = options.CrossoverRate,
                PointsNumber = 1, // Local execution
                SaveResults = false, // Workers don't save files
                OutputFile = "",
                BestRouteFile = "",
                Seed = options.Seed + Environment.CurrentManagedThreadId // Different seed for each worker
            };
            
            // Ensure minimum population size
            if (localOptions.PopulationSize < 50)
            {
                localOptions.PopulationSize = 50;
            }
            
            Console.WriteLine($"Local population size: {localOptions.PopulationSize}");
            
            var ga = new GeneticAlgorithm(cities, localOptions);
            
            Console.WriteLine("Initializing local genetic algorithm...");
            ga.Initialize();
            
            Console.WriteLine("Running local evolution...");
            ga.RunGenerations(localOptions.Generations);
            
            var bestRoute = ga.GetBestRoute();
            var averageDistance = ga.GetAverageDistance();
            var convergenceHistory = ga.GetConvergenceHistory();
            
            return new ModuleOutput
            {
                BestDistance = bestRoute.TotalDistance,
                AverageDistance = averageDistance,
                GenerationsCompleted = convergenceHistory.Count,
                BestRoute = bestRoute.Cities,
                ConvergenceHistory = convergenceHistory
            };
        }
    }
} 