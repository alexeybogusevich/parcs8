using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Tests
{
    [TestClass]
    public class CityTests
    {
        [TestMethod]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var city = new City(1, 10.5, 20.7);

            // Assert
            Assert.AreEqual(1, city.Id);
            Assert.AreEqual(10.5, city.X);
            Assert.AreEqual(20.7, city.Y);
        }

        [TestMethod]
        public void DefaultConstructor_CreatesInstance()
        {
            // Act
            var city = new City();

            // Assert
            Assert.AreEqual(0, city.Id);
            Assert.AreEqual(0, city.X);
            Assert.AreEqual(0, city.Y);
        }

        [TestMethod]
        public void DistanceTo_ValidCity_ReturnsCorrectDistance()
        {
            // Arrange
            var city1 = new City(0, 0, 0);
            var city2 = new City(1, 3, 4);

            // Act
            var distance = city1.DistanceTo(city2);

            // Assert
            // Distance should be √(3² + 4²) = 5
            Assert.AreEqual(5.0, distance, 0.001);
        }

        [TestMethod]
        public void DistanceTo_NullCity_ThrowsArgumentNullException()
        {
            // Arrange
            var city = new City(0, 0, 0);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city.DistanceTo(null));
        }

        [TestMethod]
        public void DistanceTo_SameCity_ReturnsZero()
        {
            // Arrange
            var city = new City(0, 10, 20);

            // Act
            var distance = city.DistanceTo(city);

            // Assert
            Assert.AreEqual(0.0, distance, 0.001);
        }

        [TestMethod]
        public void ManhattanDistanceTo_ValidCity_ReturnsCorrectDistance()
        {
            // Arrange
            var city1 = new City(0, 0, 0);
            var city2 = new City(1, 3, 4);

            // Act
            var distance = city1.ManhattanDistanceTo(city2);

            // Assert
            // Manhattan distance should be |3| + |4| = 7
            Assert.AreEqual(7.0, distance, 0.001);
        }

        [TestMethod]
        public void ManhattanDistanceTo_NullCity_ThrowsArgumentNullException()
        {
            // Arrange
            var city = new City(0, 0, 0);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city.ManhattanDistanceTo(null));
        }

        [TestMethod]
        public void ChebyshevDistanceTo_ValidCity_ReturnsCorrectDistance()
        {
            // Arrange
            var city1 = new City(0, 0, 0);
            var city2 = new City(1, 3, 4);

            // Act
            var distance = city1.ChebyshevDistanceTo(city2);

            // Assert
            // Chebyshev distance should be max(|3|, |4|) = 4
            Assert.AreEqual(4.0, distance, 0.001);
        }

        [TestMethod]
        public void ChebyshevDistanceTo_NullCity_ThrowsArgumentNullException()
        {
            // Arrange
            var city = new City(0, 0, 0);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city.ChebyshevDistanceTo(null));
        }

        [TestMethod]
        public void ToString_ReturnsValidString()
        {
            // Arrange
            var city = new City(1, 10.5, 20.7);

            // Act
            var result = city.ToString();

            // Assert
            Assert.AreEqual("City1(10.50, 20.70)", result);
        }

        [TestMethod]
        public void Equals_SameCity_ReturnsTrue()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(1, 10.5, 20.7);

            // Act & Assert
            Assert.AreEqual(city1, city2);
        }

        [TestMethod]
        public void Equals_DifferentId_ReturnsFalse()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(2, 10.5, 20.7);

            // Act & Assert
            Assert.AreNotEqual(city1, city2);
        }

        [TestMethod]
        public void Equals_DifferentX_ReturnsFalse()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(1, 10.6, 20.7);

            // Act & Assert
            Assert.AreNotEqual(city1, city2);
        }

        [TestMethod]
        public void Equals_DifferentY_ReturnsFalse()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(1, 10.5, 20.8);

            // Act & Assert
            Assert.AreNotEqual(city1, city2);
        }

        [TestMethod]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var city = new City(1, 10.5, 20.7);

            // Act & Assert
            Assert.AreNotEqual(city, null);
        }

        [TestMethod]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var city = new City(1, 10.5, 20.7);
            var otherObject = "not a city";

            // Act & Assert
            Assert.AreNotEqual(city, otherObject);
        }

        [TestMethod]
        public void GetHashCode_SameCity_ReturnsSameHashCode()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(1, 10.5, 20.7);

            // Act & Assert
            Assert.AreEqual(city1.GetHashCode(), city2.GetHashCode());
        }

        [TestMethod]
        public void GetHashCode_DifferentCity_ReturnsDifferentHashCode()
        {
            // Arrange
            var city1 = new City(1, 10.5, 20.7);
            var city2 = new City(2, 10.5, 20.7);

            // Act & Assert
            Assert.AreNotEqual(city1.GetHashCode(), city2.GetHashCode());
        }

        [TestMethod]
        public void OperatorMinus_ValidCities_ReturnsCorrectCity()
        {
            // Arrange
            var city1 = new City(0, 10, 20);
            var city2 = new City(1, 3, 4);

            // Act
            var result = city1 - city2;

            // Assert
            Assert.AreEqual(-1, result.Id); // Special ID for computed cities
            Assert.AreEqual(7, result.X);   // 10 - 3
            Assert.AreEqual(16, result.Y);  // 20 - 4
        }

        [TestMethod]
        public void OperatorMinus_FirstCityNull_ThrowsArgumentNullException()
        {
            // Arrange
            City city1 = null;
            var city2 = new City(1, 3, 4);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city1 - city2);
        }

        [TestMethod]
        public void OperatorMinus_SecondCityNull_ThrowsArgumentNullException()
        {
            // Arrange
            var city1 = new City(0, 10, 20);
            City city2 = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city1 - city2);
        }

        [TestMethod]
        public void OperatorPlus_ValidCities_ReturnsCorrectCity()
        {
            // Arrange
            var city1 = new City(0, 10, 20);
            var city2 = new City(1, 3, 4);

            // Act
            var result = city1 + city2;

            // Assert
            Assert.AreEqual(-1, result.Id); // Special ID for computed cities
            Assert.AreEqual(13, result.X);  // 10 + 3
            Assert.AreEqual(24, result.Y);  // 20 + 4
        }

        [TestMethod]
        public void OperatorPlus_FirstCityNull_ThrowsArgumentNullException()
        {
            // Arrange
            City city1 = null;
            var city2 = new City(1, 3, 4);

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city1 + city2);
        }

        [TestMethod]
        public void OperatorPlus_SecondCityNull_ThrowsArgumentNullException()
        {
            // Arrange
            var city1 = new City(0, 10, 20);
            City city2 = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => city1 + city2);
        }

        [TestMethod]
        public void DistanceCalculation_Precision_HandlesSmallDifferences()
        {
            // Arrange
            var city1 = new City(0, 0.000000001, 0);
            var city2 = new City(1, 0, 0);

            // Act
            var distance = city1.DistanceTo(city2);

            // Assert
            Assert.IsTrue(distance > 0);
            Assert.IsTrue(distance < 0.000000002);
        }

        [TestMethod]
        public void DistanceCalculation_LargeCoordinates_HandlesCorrectly()
        {
            // Arrange
            var city1 = new City(0, 1000000, 2000000);
            var city2 = new City(1, 1000001, 2000001);

            // Act
            var distance = city1.DistanceTo(city2);

            // Assert
            Assert.IsTrue(distance > 0);
            Assert.IsTrue(distance < 2); // Should be approximately √2
        }
    }
} 