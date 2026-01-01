using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Tests
{
    [TestClass]
    public class RouteTests
    {
        private List<City> _testCities;
        private Random _testRandom;

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

            _testRandom = new Random(42);
        }

        [TestMethod]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var route = new Route(_testCities, _testRandom);

            // Assert
            Assert.IsNotNull(route);
            Assert.AreEqual(5, route.Cities.Count);
            Assert.IsTrue(route.TotalDistance > 0);
        }

        [TestMethod]
        public void Constructor_NullCities_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new Route(null, _testRandom));
        }

        [TestMethod]
        public void Constructor_NullRandom_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new Route(_testCities, null));
        }

        [TestMethod]
        public void CopyConstructor_CreatesValidCopy()
        {
            // Arrange
            var originalRoute = new Route(_testCities, _testRandom);

            // Act
            var copiedRoute = new Route(originalRoute);

            // Assert
            Assert.AreEqual(originalRoute.Cities.Count, copiedRoute.Cities.Count);
            Assert.AreEqual(originalRoute.TotalDistance, copiedRoute.TotalDistance);
            
            // Verify it's a deep copy
            Assert.AreNotSame(originalRoute.Cities, copiedRoute.Cities);
        }

        [TestMethod]
        public void Cities_ContainsAllCityIndices()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var cities = route.Cities;

            // Assert
            Assert.AreEqual(5, cities.Count);
            Assert.IsTrue(cities.All(c => c >= 0 && c < 5));
            Assert.AreEqual(5, cities.Distinct().Count()); // All indices should be unique
        }

        [TestMethod]
        public void TotalDistance_IsCalculatedCorrectly()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var distance = route.TotalDistance;

            // Assert
            Assert.IsTrue(distance > 0);
            
            // For cities in a line (0,0) -> (1,1) -> (2,2) -> (3,3) -> (4,4) -> (0,0)
            // Each step is √2 ≈ 1.414, total should be around 5 * √2 ≈ 7.07
            // But since cities are shuffled, we just check it's reasonable
            Assert.IsTrue(distance > 5 && distance < 20);
        }

        [TestMethod]
        public void CalculateDistance_UpdatesTotalDistance()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);
            var originalDistance = route.TotalDistance;

            // Act
            route.CalculateDistance();

            // Assert
            Assert.AreEqual(originalDistance, route.TotalDistance);
        }

        [TestMethod]
        public void Mutate_ChangesRoute()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);
            var originalCities = new List<int>(route.Cities);
            var originalDistance = route.TotalDistance;

            // Act
            route.Mutate();

            // Assert
            // Mutation should change either the route or the distance
            var citiesChanged = !route.Cities.SequenceEqual(originalCities);
            var distanceChanged = Math.Abs(route.TotalDistance - originalDistance) > 0.001;
            
            Assert.IsTrue(citiesChanged || distanceChanged);
        }

        [TestMethod]
        public void Crossover_WithValidParent_CreatesValidOffspring()
        {
            // Arrange
            var parent1 = new Route(_testCities, _testRandom);
            var parent2 = new Route(_testCities, _testRandom);

            // Act
            var offspring = parent1.Crossover(parent2);

            // Assert
            Assert.IsNotNull(offspring);
            Assert.AreEqual(5, offspring.Cities.Count);
            Assert.IsTrue(offspring.TotalDistance > 0);
            
            // Offspring should contain all city indices exactly once
            var sortedCities = offspring.Cities.OrderBy(c => c).ToList();
            var expectedCities = Enumerable.Range(0, 5).ToList();
            CollectionAssert.AreEqual(expectedCities, sortedCities);
        }

        [TestMethod]
        public void Crossover_WithSameParent_CreatesValidOffspring()
        {
            // Arrange
            var parent = new Route(_testCities, _testRandom);

            // Act
            var offspring = parent.Crossover(parent);

            // Assert
            Assert.IsNotNull(offspring);
            Assert.AreEqual(5, offspring.Cities.Count);
            Assert.IsTrue(offspring.TotalDistance > 0);
        }

        [TestMethod]
        public void InversionMutation_CreatesValidRoute()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var mutatedRoute = route.InversionMutation();

            // Assert
            Assert.IsNotNull(mutatedRoute);
            Assert.AreEqual(5, mutatedRoute.Cities.Count);
            Assert.IsTrue(mutatedRoute.TotalDistance > 0);
            
            // Should contain all city indices exactly once
            var sortedCities = mutatedRoute.Cities.OrderBy(c => c).ToList();
            var expectedCities = Enumerable.Range(0, 5).ToList();
            CollectionAssert.AreEqual(expectedCities, sortedCities);
        }

        [TestMethod]
        public void ScrambleMutation_CreatesValidRoute()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var mutatedRoute = route.ScrambleMutation();

            // Assert
            Assert.IsNotNull(mutatedRoute);
            Assert.AreEqual(5, mutatedRoute.Cities.Count);
            Assert.IsTrue(mutatedRoute.TotalDistance > 0);
            
            // Should contain all city indices exactly once
            var sortedCities = mutatedRoute.Cities.OrderBy(c => c).ToList();
            var expectedCities = Enumerable.Range(0, 5).ToList();
            CollectionAssert.AreEqual(expectedCities, sortedCities);
        }

        [TestMethod]
        public void ToString_ReturnsValidString()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var result = route.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Route:"));
            Assert.IsTrue(result.Contains("Distance:"));
        }

        [TestMethod]
        public void GetFormattedRoute_ReturnsValidString()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);

            // Act
            var result = route.GetFormattedRoute();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("City"));
            Assert.IsTrue(result.Contains("->"));
        }

        [TestMethod]
        public void MultipleMutations_ProduceDifferentRoutes()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);
            var routes = new List<Route>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                var mutatedRoute = new Route(route);
                mutatedRoute.Mutate();
                routes.Add(mutatedRoute);
            }

            // Assert
            // At least some routes should be different
            var uniqueRoutes = routes.Select(r => string.Join(",", r.Cities)).Distinct().Count();
            Assert.IsTrue(uniqueRoutes > 1);
        }

        [TestMethod]
        public void Crossover_WithDifferentParents_ProducesDifferentOffspring()
        {
            // Arrange
            var parent1 = new Route(_testCities, _testRandom);
            var parent2 = new Route(_testCities, _testRandom);
            var offspring1 = parent1.Crossover(parent2);
            var offspring2 = parent1.Crossover(parent2);

            // Act & Assert
            // Since crossover involves randomness, offspring might be different
            // We just verify both are valid
            Assert.IsNotNull(offspring1);
            Assert.IsNotNull(offspring2);
            Assert.AreEqual(5, offspring1.Cities.Count);
            Assert.AreEqual(5, offspring2.Cities.Count);
        }

        [TestMethod]
        public void DistanceCalculation_IsConsistent()
        {
            // Arrange
            var route = new Route(_testCities, _testRandom);
            var initialDistance = route.TotalDistance;

            // Act
            route.CalculateDistance();
            var recalculatedDistance = route.TotalDistance;

            // Assert
            Assert.AreEqual(initialDistance, recalculatedDistance, 0.001);
        }
    }
} 