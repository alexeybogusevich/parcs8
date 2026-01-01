using Parcs.Net;
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
        
        // Нові опції для завантаження з файлу
        public bool LoadFromFile { get; set; } = false;
        public string InputFile { get; set; } = "cities.txt";
        public bool GenerateRandomCities { get; set; } = true;

        /// <summary>
        /// Автоматично налаштовує параметри для великих задач
        /// </summary>
        public void AutoConfigureForLargeProblems()
        {
            // Визначаємо розмір задачі на основі кількості міст
            var problemSize = GetProblemSize();
            
            switch (problemSize)
            {
                case ProblemSize.Small: // 1-100 міст
                    ConfigureForSmallProblem();
                    break;
                case ProblemSize.Medium: // 101-500 міст
                    ConfigureForMediumProblem();
                    break;
                case ProblemSize.Large: // 501-1000 міст
                    ConfigureForLargeProblem();
                    break;
                case ProblemSize.ExtraLarge: // 1000+ міст
                    ConfigureForExtraLargeProblem();
                    break;
            }
        }
        
        /// <summary>
        /// Визначає розмір задачі на основі кількості міст
        /// </summary>
        private ProblemSize GetProblemSize()
        {
            if (CitiesNumber <= 100) return ProblemSize.Small;
            if (CitiesNumber <= 500) return ProblemSize.Medium;
            if (CitiesNumber <= 1000) return ProblemSize.Large;
            return ProblemSize.ExtraLarge;
        }
        
        /// <summary>
        /// Налаштування для малих задач
        /// </summary>
        private void ConfigureForSmallProblem()
        {
            PopulationSize = Math.Max(200, PopulationSize);
            Generations = Math.Max(50, Generations);
            MutationRate = Math.Max(0.01, MutationRate);
            CrossoverRate = Math.Max(0.8, CrossoverRate);
            PointsNumber = Math.Max(2, Math.Min(4, PointsNumber));
        }
        
        /// <summary>
        /// Налаштування для середніх задач
        /// </summary>
        private void ConfigureForMediumProblem()
        {
            PopulationSize = Math.Max(300, Math.Min(800, PopulationSize));
            Generations = Math.Max(75, Math.Min(150, Generations));
            MutationRate = Math.Max(0.008, Math.Min(0.015, MutationRate));
            CrossoverRate = Math.Max(0.75, Math.Min(0.85, CrossoverRate));
            PointsNumber = Math.Max(3, Math.Min(6, PointsNumber));
        }
        
        /// <summary>
        /// Налаштування для великих задач
        /// </summary>
        private void ConfigureForLargeProblem()
        {
            PopulationSize = Math.Max(400, Math.Min(1000, PopulationSize));
            Generations = Math.Max(100, Math.Min(200, Generations));
            MutationRate = Math.Max(0.005, Math.Min(0.012, MutationRate));
            CrossoverRate = Math.Max(0.7, Math.Min(0.8, CrossoverRate));
            PointsNumber = Math.Max(4, Math.Min(8, PointsNumber));
        }
        
        /// <summary>
        /// Налаштування для дуже великих задач
        /// </summary>
        private void ConfigureForExtraLargeProblem()
        {
            PopulationSize = Math.Max(500, Math.Min(1200, PopulationSize));
            Generations = Math.Max(150, Math.Min(300, Generations));
            MutationRate = Math.Max(0.003, Math.Min(0.01, MutationRate));
            CrossoverRate = Math.Max(0.65, Math.Min(0.75, CrossoverRate));
            PointsNumber = Math.Max(6, Math.Min(12, PointsNumber));
        }
        
        /// <summary>
        /// Розмір задачі TSP
        /// </summary>
        private enum ProblemSize
        {
            Small,      // 1-100 міст
            Medium,     // 101-500 міст
            Large,      // 501-1000 міст
            ExtraLarge  // 1000+ міст
        }
        
        /// <summary>
        /// Створює оптимізовані опції для заданої кількості міст
        /// </summary>
        public static ModuleOptions CreateOptimized(int citiesNumber, int? populationSize = null, int? generations = null, int? pointsNumber = null)
        {
            var options = new ModuleOptions
            {
                CitiesNumber = citiesNumber,
                PopulationSize = populationSize ?? GetDefaultPopulationSize(citiesNumber),
                Generations = generations ?? GetDefaultGenerations(citiesNumber),
                PointsNumber = pointsNumber ?? GetDefaultPointsNumber(citiesNumber),
            };
            
            // Автоматично налаштовуємо параметри
            options.AutoConfigureForLargeProblems();
            
            return options;
        }
        
        /// <summary>
        /// Отримує рекомендований розмір популяції для заданої кількості міст
        /// </summary>
        private static int GetDefaultPopulationSize(int citiesNumber)
        {
            if (citiesNumber <= 100) return 200;
            if (citiesNumber <= 500) return 400;
            if (citiesNumber <= 1000) return 600;
            return 800;
        }
        
        /// <summary>
        /// Отримує рекомендовану кількість поколінь для заданої кількості міст
        /// </summary>
        private static int GetDefaultGenerations(int citiesNumber)
        {
            if (citiesNumber <= 100) return 80;
            if (citiesNumber <= 500) return 120;
            if (citiesNumber <= 1000) return 180;
            return 250;
        }
        
        /// <summary>
        /// Отримує рекомендовану кількість точок для заданої кількості міст
        /// </summary>
        private static int GetDefaultPointsNumber(int citiesNumber)
        {
            if (citiesNumber <= 100) return 2;
            if (citiesNumber <= 500) return 4;
            if (citiesNumber <= 1000) return 6;
            return 8;
        }
        
        /// <summary>
        /// Створює оптимізовані опції для завантаження з файлу
        /// </summary>
        public static ModuleOptions CreateOptimizedForFile(string inputFile, int? populationSize = null, int? generations = null, int? pointsNumber = null)
        {
            var options = new ModuleOptions
            {
                LoadFromFile = true,
                InputFile = inputFile,
                GenerateRandomCities = false
            };
            
            // Оцінюємо розмір задачі на основі назви файлу
            var estimatedCities = EstimateCitiesFromFileName(inputFile);
            options.CitiesNumber = estimatedCities;
            
            // Застосовуємо оптимізацію
            options.PopulationSize = populationSize ?? GetDefaultPopulationSize(estimatedCities);
            options.Generations = generations ?? GetDefaultGenerations(estimatedCities);
            options.PointsNumber = pointsNumber ?? GetDefaultPointsNumber(estimatedCities);
            
            options.AutoConfigureForLargeProblems();
            
            return options;
        }
        
        /// <summary>
        /// Оцінює кількість міст на основі назви файлу
        /// </summary>
        private static int EstimateCitiesFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return 100;
            
            var lowerFileName = fileName.ToLowerInvariant();
            
            if (lowerFileName.Contains("cities_1000") || lowerFileName.Contains("1000"))
                return 1000;
            if (lowerFileName.Contains("cities_500") || lowerFileName.Contains("500"))
                return 500;
            if (lowerFileName.Contains("cities_250") || lowerFileName.Contains("250"))
                return 250;
            if (lowerFileName.Contains("cities_100") || lowerFileName.Contains("100"))
                return 100;
            if (lowerFileName.Contains("cities_50") || lowerFileName.Contains("50"))
                return 50;
            
            // За замовчуванням
            return 100;
        }
    }
} 