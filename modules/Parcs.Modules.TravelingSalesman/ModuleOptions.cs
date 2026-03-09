using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;
namespace Parcs.Modules.TravelingSalesman
{
    public class ModuleOptions : IModuleOptions
    {
        public int CitiesNumber { get; set; } = 50;
        public int PopulationSize { get; set; } = 1000;
        public int Generations { get; set; } = 100;
        public double MutationRate { get; set; } = 0.01;
        public double CrossoverRate { get; set; } = 0.8;
        public int PointsNumber { get; set; } = 4;
        public bool SaveResults { get; set; } = true;
        public string OutputFile { get; set; } = "tsp_results.json";
        public string BestRouteFile { get; set; } = "best_route.txt";
        public int Seed { get; set; } = 42;

        public bool LoadFromFile { get; set; } = false;
        public string InputFile { get; set; } = "cities.txt";
        public bool GenerateRandomCities { get; set; } = true;

        // Island Model with Migration options
        public bool EnableMigration { get; set; } = false;
        public MigrationType MigrationType { get; set; } = MigrationType.BestIndividuals;
        public int MigrationSize { get; set; } = 5;
        public int MigrationInterval { get; set; } = 10;
    }
}
