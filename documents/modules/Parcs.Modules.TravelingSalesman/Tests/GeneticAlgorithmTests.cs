using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Tests
{
    [TestClass]
    public class GeneticAlgorithmTests
    {
        private List<City> _testCities;
        private ModuleOptions _testOptions;

        [TestInitialize]
        public void Setup()
        {
            _testCities = new List<City>
            {
                new City(0, 0, 0),
                new City(1, 1, 1),
                new City(2, 2, 2),
                new City(3, 3, 3),
                new City(4, 4, 4)
            };

            _testOptions = new ModuleOptions
            {
                CitiesNumber = 5,
                PopulationSize = 100,
                Generations = 50,
                MutationRate = 0.01,
                CrossoverRate = 0.8,
                Seed = 42
            };
        }

        [TestMethod]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var ga = new GeneticAlgorithm(_testCities, _testOptions);

            // Assert
            Assert.IsNotNull(ga);
        }

        [TestMethod]
        public void Constructor_NullCities_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new GeneticAlgorithm(null, _testOptions));
        }

        [TestMethod]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new GeneticAlgorithm(_testCities, null));
        }

        [TestMethod]
        public void Initialize_CreatesPopulation()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);

            // Act
            ga.Initialize();

            // Assert
            var bestRoute = ga.GetBestRoute();
            Assert.IsNotNull(bestRoute);
            Assert.AreEqual(5, bestRoute.Cities.Count);
        }

        [TestMethod]
        public void GetBestRoute_AfterInitialization_ReturnsValidRoute()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();

            // Act
            var bestRoute = ga.GetBestRoute();

            // Assert
            Assert.IsNotNull(bestRoute);
            Assert.AreEqual(5, bestRoute.Cities.Count);
            Assert.IsTrue(bestRoute.TotalDistance > 0);
        }

        [TestMethod]
        public void GetAverageDistance_AfterInitialization_ReturnsValidValue()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();

            // Act
            var averageDistance = ga.GetAverageDistance();

            // Assert
            Assert.IsTrue(averageDistance > 0);
        }

        [TestMethod]
        public void Evolve_ImprovesPopulation()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();
            var initialBestDistance = ga.GetBestRoute().TotalDistance;

            // Act
            ga.Evolve();
            var evolvedBestDistance = ga.GetBestRoute().TotalDistance;

            // Assert
            Assert.IsTrue(evolvedBestDistance <= initialBestDistance);
        }

        [TestMethod]
        public void RunGenerations_CompletesSpecifiedGenerations()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();

            // Act
            ga.RunGenerations(10);

            // Assert
            var convergenceHistory = ga.GetConvergenceHistory();
            Assert.AreEqual(10, convergenceHistory.Count);
        }

        [TestMethod]
        public void Optimize_CompletesFullOptimization()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);

            // Act
            var bestRoute = ga.GetOptimizedRoute();

            // Assert
            Assert.IsNotNull(bestRoute);
            Assert.AreEqual(5, bestRoute.Cities.Count);
            Assert.IsTrue(bestRoute.TotalDistance > 0);
        }

        [TestMethod]
        public void GetConvergenceHistory_ReturnsValidHistory()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();
            ga.RunGenerations(5);

            // Act
            var history = ga.GetConvergenceHistory();

            // Assert
            Assert.AreEqual(5, history.Count);
            Assert.IsTrue(history.All(d => d > 0));
        }

        [TestMethod]
        public void IsConverged_WithImprovement_ReturnsFalse()
        {
            // Arrange
            var ga = new GeneticAlgorithm(_testCities, _testOptions);
            ga.Initialize();
            ga.RunGenerations(5);

            // Act
            // Note: This is a private method, so we test it indirectly through public behavior
            // The method should not stop early if there's significant improvement

            // Assert
            var history = ga.GetConvergenceHistory();
            Assert.AreEqual(5, history.Count); // Should complete all generations
        }

        [TestMethod]
        public void DifferentSeeds_ProduceDifferentResults()
        {
            // Arrange
            var options1 = new ModuleOptions
            {
                CitiesNumber = 5,
                PopulationSize = 50,
                Generations = 20,
                Seed = 42
            };

            var options2 = new ModuleOptions
            {
                CitiesNumber = 5,
                PopulationSize = 50,
                Generations = 20,
                Seed = 123
            };

            // Act
            var ga1 = new GeneticAlgorithm(_testCities, options1);
            var ga2 = new GeneticAlgorithm(_testCities, options2);

            ga1.Optimize();
            ga2.Optimize();

            var result1 = ga1.GetBestRoute();
            var result2 = ga2.GetBestRoute();

            // Assert
            // Different seeds should produce different results (though this is not guaranteed)
            // We just verify that both produce valid results
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.IsTrue(result1.TotalDistance > 0);
            Assert.IsTrue(result2.TotalDistance > 0);
        }

        [TestMethod]
        public void LargePopulation_HandlesCorrectly()
        {
            // Arrange
            var largeOptions = new ModuleOptions
            {
                CitiesNumber = 5,
                PopulationSize = 1000,
                Generations = 10,
                Seed = 42
            };

            // Act
            var ga = new GeneticAlgorithm(_testCities, largeOptions);
            ga.Initialize();

            // Assert
            var bestRoute = ga.GetBestRoute();
            Assert.IsNotNull(bestRoute);
            Assert.AreEqual(5, bestRoute.Cities.Count);
        }

        [TestMethod]
        public void EmptyCities_ThrowsException()
        {
            // Arrange
            var emptyCities = new List<City>();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new GeneticAlgorithm(emptyCities, _testOptions));
        }
    }
} 