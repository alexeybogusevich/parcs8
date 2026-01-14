namespace Parcs.Modules.TravelingSalesman.Examples
{
    /// <summary>
    /// Тест автоматичної конфігурації параметрів для різних розмірів задач
    /// </summary>
    public static class AutoConfigurationTest
    {
        public static void TestAutoConfiguration()
        {
            Console.WriteLine("=== Тест автоматичної конфігурації параметрів ===\n");
            
            // Тестуємо різні розміри задач
            TestConfiguration(50, "Мала задача");
            TestConfiguration(250, "Середня задача");
            TestConfiguration(750, "Велика задача");
            TestConfiguration(1500, "Дуже велика задача");
            
            Console.WriteLine("\n=== Тест створення оптимізованих опцій ===\n");
            
            // Тестуємо створення оптимізованих опцій
            TestCreateOptimized(100, "100 міст");
            TestCreateOptimized(500, "500 міст");
            TestCreateOptimized(1000, "1000 міст");
        }
        
        private static void TestConfiguration(int citiesNumber, string description)
        {
            Console.WriteLine($"--- {description} ({citiesNumber} міст) ---");
            
            var options = new ModuleOptions
            {
                CitiesNumber = citiesNumber,
                PopulationSize = 1000, // Початкове значення
                Generations = 200,     // Початкове значення
                PointsNumber = 8,      // Початкове значення
            };
            
            Console.WriteLine($"До оптимізації:");
            Console.WriteLine($"  PopulationSize: {options.PopulationSize}");
            Console.WriteLine($"  Generations: {options.Generations}");
            Console.WriteLine($"  PointsNumber: {options.PointsNumber}");
            
            // Застосовуємо автоматичну оптимізацію
            options.AutoConfigureForLargeProblems();
            
            Console.WriteLine($"\nПісля оптимізації:");
            Console.WriteLine($"  PopulationSize: {options.PopulationSize}");
            Console.WriteLine($"  Generations: {options.Generations}");
            Console.WriteLine($"  PointsNumber: {options.PointsNumber}");
            Console.WriteLine();
        }
        
        private static void TestCreateOptimized(int citiesNumber, string description)
        {
            Console.WriteLine($"--- {description} ---");
            
            var options = ModuleOptions.CreateOptimized(citiesNumber);
            
            Console.WriteLine($"Автоматично створені опції:");
            Console.WriteLine($"  CitiesNumber: {options.CitiesNumber}");
            Console.WriteLine($"  PopulationSize: {options.PopulationSize}");
            Console.WriteLine($"  Generations: {options.Generations}");
            Console.WriteLine($"  PointsNumber: {options.PointsNumber}");
            Console.WriteLine();
        }
        
        /// <summary>
        /// Тестує різні типи міграції
        /// </summary>
        public static void TestMigrationTypes()
        {
            Console.WriteLine("=== Тест типів міграції ===\n");
            
            var citiesNumber = 500;
            var options = ModuleOptions.CreateOptimized(citiesNumber);
            
            // Тестуємо різні типи міграції
            var migrationTypes = new[] 
            { 
                Models.MigrationType.BestIndividuals,
                Models.MigrationType.RandomIndividuals,
                Models.MigrationType.DiverseIndividuals,
                Models.MigrationType.TournamentSelection
            };
            
            foreach (var migrationType in migrationTypes)
            {
                options.AutoConfigureForLargeProblems();
                
                Console.WriteLine($"Тип міграції: {migrationType}");
                Console.WriteLine();
            }
        }
    }
} 