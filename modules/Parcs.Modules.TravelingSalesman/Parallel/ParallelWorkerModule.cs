using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var cities = await moduleInfo.Parent.ReadObjectAsync<List<City>>();
            var options = moduleInfo.BindModuleOptions<ModuleOptions>();

            var stopwatch = Stopwatch.StartNew();
            var result = RunLocalGeneticAlgorithm(cities, options);

            stopwatch.Stop();
            result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            await moduleInfo.Parent.WriteObjectAsync(result);
        }

        private static ModuleOutput RunLocalGeneticAlgorithm(List<City> cities, ModuleOptions options)
        {
            var localOptions = new ModuleOptions
            {
                CitiesNumber = options.CitiesNumber,
                PopulationSize = Math.Max(options.PopulationSize / options.PointsNumber, 50),
                Generations = options.Generations,
                MutationRate = options.MutationRate,
                CrossoverRate = options.CrossoverRate,
                PointsNumber = 1,
                SaveResults = false,
                OutputFile = "",
                BestRouteFile = "",
                Seed = options.Seed + Environment.CurrentManagedThreadId
            };
            
            var ga = new GeneticAlgorithm(cities, localOptions);
            
            ga.Initialize();
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