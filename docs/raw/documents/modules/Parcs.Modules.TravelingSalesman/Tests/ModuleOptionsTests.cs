using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parcs.Modules.TravelingSalesman;

namespace Parcs.Modules.TravelingSalesman.Tests
{
    [TestClass]
    public class ModuleOptionsTests
    {
        [TestMethod]
        public void DefaultValues_AreSetCorrectly()
        {
            // Act
            var options = new ModuleOptions();

            // Assert
            Assert.AreEqual(50, options.CitiesNumber);
            Assert.AreEqual(1000, options.PopulationSize);
            Assert.AreEqual(100, options.Generations);
            Assert.AreEqual(0.01, options.MutationRate);
            Assert.AreEqual(0.8, options.CrossoverRate);
            Assert.AreEqual(4, options.PointsNumber);
            Assert.IsTrue(options.SaveResults);
            Assert.AreEqual("tsp_results.json", options.OutputFile);
            Assert.AreEqual("best_route.txt", options.BestRouteFile);
            Assert.AreEqual(42, options.Seed);
        }

        [TestMethod]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.CitiesNumber = 100;
            options.PopulationSize = 2000;
            options.Generations = 200;
            options.MutationRate = 0.02;
            options.CrossoverRate = 0.9;
            options.PointsNumber = 8;
            options.SaveResults = false;
            options.OutputFile = "custom_output.json";
            options.BestRouteFile = "custom_route.txt";
            options.Seed = 123;

            // Assert
            Assert.AreEqual(100, options.CitiesNumber);
            Assert.AreEqual(2000, options.PopulationSize);
            Assert.AreEqual(200, options.Generations);
            Assert.AreEqual(0.02, options.MutationRate);
            Assert.AreEqual(0.9, options.CrossoverRate);
            Assert.AreEqual(8, options.PointsNumber);
            Assert.IsFalse(options.SaveResults);
            Assert.AreEqual("custom_output.json", options.OutputFile);
            Assert.AreEqual("custom_route.txt", options.BestRouteFile);
            Assert.AreEqual(123, options.Seed);
        }

        [TestMethod]
        public void MutationRate_ValidRange()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act & Assert
            // Valid range for mutation rate is typically [0, 1]
            options.MutationRate = 0.0;
            Assert.AreEqual(0.0, options.MutationRate);

            options.MutationRate = 0.5;
            Assert.AreEqual(0.5, options.MutationRate);

            options.MutationRate = 1.0;
            Assert.AreEqual(1.0, options.MutationRate);
        }

        [TestMethod]
        public void CrossoverRate_ValidRange()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act & Assert
            // Valid range for crossover rate is typically [0, 1]
            options.CrossoverRate = 0.0;
            Assert.AreEqual(0.0, options.CrossoverRate);

            options.CrossoverRate = 0.5;
            Assert.AreEqual(0.5, options.CrossoverRate);

            options.CrossoverRate = 1.0;
            Assert.AreEqual(1.0, options.CrossoverRate);
        }

        [TestMethod]
        public void CitiesNumber_PositiveValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.CitiesNumber = 25;

            // Assert
            Assert.AreEqual(25, options.CitiesNumber);
            Assert.IsTrue(options.CitiesNumber > 0);
        }

        [TestMethod]
        public void PopulationSize_PositiveValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.PopulationSize = 500;

            // Assert
            Assert.AreEqual(500, options.PopulationSize);
            Assert.IsTrue(options.PopulationSize > 0);
        }

        [TestMethod]
        public void Generations_PositiveValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.Generations = 150;

            // Assert
            Assert.AreEqual(150, options.Generations);
            Assert.IsTrue(options.Generations > 0);
        }

        [TestMethod]
        public void PointsNumber_PositiveValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.PointsNumber = 6;

            // Assert
            Assert.AreEqual(6, options.PointsNumber);
            Assert.IsTrue(options.PointsNumber > 0);
        }

        [TestMethod]
        public void Seed_CanBeSet()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.Seed = 999;

            // Assert
            Assert.AreEqual(999, options.Seed);
        }

        [TestMethod]
        public void SaveResults_BooleanValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.SaveResults = false;

            // Assert
            Assert.IsFalse(options.SaveResults);

            options.SaveResults = true;
            Assert.IsTrue(options.SaveResults);
        }

        [TestMethod]
        public void OutputFile_StringValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.OutputFile = "my_results.json";

            // Assert
            Assert.AreEqual("my_results.json", options.OutputFile);
        }

        [TestMethod]
        public void BestRouteFile_StringValue()
        {
            // Arrange
            var options = new ModuleOptions();

            // Act
            options.BestRouteFile = "my_route.txt";

            // Assert
            Assert.AreEqual("my_route.txt", options.BestRouteFile);
        }

        [TestMethod]
        public void MultipleInstances_AreIndependent()
        {
            // Arrange
            var options1 = new ModuleOptions();
            var options2 = new ModuleOptions();

            // Act
            options1.CitiesNumber = 100;
            options2.CitiesNumber = 200;

            // Assert
            Assert.AreEqual(100, options1.CitiesNumber);
            Assert.AreEqual(200, options2.CitiesNumber);
            Assert.AreNotEqual(options1.CitiesNumber, options2.CitiesNumber);
        }

        [TestMethod]
        public void Configuration_ForSmallProblem()
        {
            // Arrange
            var options = new ModuleOptions
            {
                CitiesNumber = 10,
                PopulationSize = 100,
                Generations = 50,
                MutationRate = 0.05,
                CrossoverRate = 0.7,
                PointsNumber = 2
            };

            // Assert
            Assert.AreEqual(10, options.CitiesNumber);
            Assert.AreEqual(100, options.PopulationSize);
            Assert.AreEqual(50, options.Generations);
            Assert.AreEqual(0.05, options.MutationRate);
            Assert.AreEqual(0.7, options.CrossoverRate);
            Assert.AreEqual(2, options.PointsNumber);
        }

        [TestMethod]
        public void Configuration_ForLargeProblem()
        {
            // Arrange
            var options = new ModuleOptions
            {
                CitiesNumber = 1000,
                PopulationSize = 10000,
                Generations = 500,
                MutationRate = 0.001,
                CrossoverRate = 0.9,
                PointsNumber = 16
            };

            // Assert
            Assert.AreEqual(1000, options.CitiesNumber);
            Assert.AreEqual(10000, options.PopulationSize);
            Assert.AreEqual(500, options.Generations);
            Assert.AreEqual(0.001, options.MutationRate);
            Assert.AreEqual(0.9, options.CrossoverRate);
            Assert.AreEqual(16, options.PointsNumber);
        }
    }
} 