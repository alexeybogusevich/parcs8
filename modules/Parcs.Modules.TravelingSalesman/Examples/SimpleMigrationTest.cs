using System;
using System.Collections.Generic;
using System.Linq;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Examples
{
    /// <summary>
    /// Простий тест для перевірки роботи міграції
    /// </summary>
    public static class SimpleMigrationTest
    {
        public static void TestBasicMigration()
        {
            Console.WriteLine("=== Простий тест міграції ===");
            
            try
            {
                // Створюємо тестові міста
                var cities = CityLoader.GenerateTestCities(50, 42, TestCityPattern.Random);
                Console.WriteLine($"Створено {cities.Count} тестових міст");
                
                // Створюємо опції з міграцією
                var options = new ModuleOptions
                {
                    CitiesNumber = cities.Count,
                    PopulationSize = 100,
                    Generations = 20,
                    MutationRate = 0.01,
                    CrossoverRate = 0.8,
                    PointsNumber = 1,
                };
                
                Console.WriteLine($"Параметри: Population={options.PopulationSize}, Generations={options.Generations}");
                
                // Створюємо GA з міграцією
                var ga = new GeneticAlgorithm(cities, options);
                
                // Ініціалізуємо популяцію
                ga.Initialize();
                Console.WriteLine("Популяція ініціалізована");
                
                // Запускаємо еволюцію
                ga.RunGenerations(options.Generations);
                Console.WriteLine("Еволюція завершена");
                
                // Отримуємо результати
                var bestRoute = ga.GetBestRoute();
                var averageDistance = ga.GetAverageDistance();
                var convergenceHistory = ga.GetConvergenceHistory();
                
                Console.WriteLine($"\nРезультати:");
                Console.WriteLine($"  Найкраща відстань: {bestRoute.TotalDistance:F2}");
                Console.WriteLine($"  Середня відстань: {averageDistance:F2}");
                Console.WriteLine($"  Поколінь виконано: {convergenceHistory.Count}");
                
                // Перевіряємо міграційного менеджера
                var migrationManager = ga.GetMigrationManager();
                if (migrationManager != null)
                {
                    Console.WriteLine($"  Міграція увімкнена: {migrationManager.MigrationType}");
                    Console.WriteLine($"  Розмір міграції: {migrationManager.MigrationSize}");
                    Console.WriteLine($"  Інтервал міграції: {migrationManager.MigrationInterval}");
                }
                else
                {
                    Console.WriteLine("  Міграція не увімкнена");
                }
                
                Console.WriteLine("✓ Простий тест міграції пройшов успішно");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Помилка під час тестування міграції: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Запускає всі тести
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("🚀 Запуск всіх тестів міграції та автоматичної конфігурації\n");
            
            // Тест автоматичної конфігурації
            AutoConfigurationTest.TestAutoConfiguration();
            Console.WriteLine();
            
            // Тест типів міграції
            AutoConfigurationTest.TestMigrationTypes();
            Console.WriteLine();
            
            // Тест базової міграції
            TestBasicMigration();
            
            Console.WriteLine("\n🎉 Всі тести завершено!");
        }
    }
} 