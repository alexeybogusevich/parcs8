using System;
using System.Collections.Generic;
using System.Linq;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public class Route
    {
        private readonly List<City> _cities;
        private readonly Random _random;
        
        public List<int> Cities { get; set; }
        public double TotalDistance { get; private set; }
        
        /// <summary>
        /// Sets the total distance. Used for Master-Slave parallel GA where
        /// distance is calculated on workers and set by master.
        /// </summary>
        public void SetDistance(double distance)
        {
            TotalDistance = distance;
        }

        /// <summary>
        /// Sets the cities list for a route. Used after deserialization in migration scenarios.
        /// Note: This uses reflection to set the private _cities field.
        /// </summary>
        public void SetCities(List<City> cities)
        {
            if (cities == null) throw new ArgumentNullException(nameof(cities));
            if (cities.Count != Cities.Count)
                throw new ArgumentException("Cities list size must match route cities count");
            
            // Use reflection to set private field (not ideal, but needed for migration)
            var field = typeof(Route).GetField("_cities", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(this, cities);
                // Recalculate distance with new cities reference
                CalculateDistance();
            }
        }

        public Route(List<City> cities, Random random)
        {
            _cities = cities ?? throw new ArgumentNullException(nameof(cities));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            
            // Generate random permutation of cities
            Cities = Enumerable.Range(0, cities.Count).ToList();
            Shuffle();
            CalculateDistance();
        }

        public Route(Route other)
        {
            _cities = other._cities;
            _random = other._random;
            Cities = new List<int>(other.Cities);
            TotalDistance = other.TotalDistance;
        }

        /// <summary>
        /// Creates a Route from an existing route without calculating distance.
        /// Used for Master-Slave parallel GA where distance is calculated on workers.
        /// </summary>
        public Route(Route other, bool skipDistanceCalculation)
        {
            _cities = other._cities;
            _random = other._random;
            Cities = new List<int>(other.Cities);
            TotalDistance = skipDistanceCalculation ? 0 : other.TotalDistance;
        }

        /// <summary>
        /// Creates a Route with a given city permutation without calculating distance.
        /// Used for Master-Slave parallel GA where distance is calculated on workers.
        /// </summary>
        public Route(List<City> cities, Random random, List<int> cityPermutation, bool skipDistanceCalculation = false)
        {
            _cities = cities ?? throw new ArgumentNullException(nameof(cities));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Cities = cityPermutation ?? throw new ArgumentNullException(nameof(cityPermutation));
            
            if (!skipDistanceCalculation)
            {
                CalculateDistance();
            }
            else
            {
                TotalDistance = 0;
            }
        }

        private void Shuffle()
        {
            int n = Cities.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                int value = Cities[k];
                Cities[k] = Cities[n];
                Cities[n] = value;
            }
        }

        public void CalculateDistance()
        {
            TotalDistance = 0;
            for (int i = 0; i < Cities.Count; i++)
            {
                int currentCityIndex = Cities[i];
                int nextCityIndex = Cities[(i + 1) % Cities.Count];
                
                TotalDistance += _cities[currentCityIndex].DistanceTo(_cities[nextCityIndex]);
            }
        }

        public void Mutate()
        {
            Mutate(skipDistanceCalculation: false);
        }

        /// <summary>
        /// Swap mutation: randomly swap two cities.
        /// </summary>
        /// <param name="skipDistanceCalculation">If true, doesn't calculate distance (for parallel GA).</param>
        public void Mutate(bool skipDistanceCalculation)
        {
            // Swap mutation: randomly swap two cities
            int index1 = _random.Next(Cities.Count);
            int index2 = _random.Next(Cities.Count);
            
            if (index1 != index2)
            {
                int temp = Cities[index1];
                Cities[index1] = Cities[index2];
                Cities[index2] = temp;
                
                if (!skipDistanceCalculation)
                {
                    CalculateDistance();
                }
                else
                {
                    TotalDistance = 0; // Will be calculated in parallel
                }
            }
        }

        public Route Crossover(Route other)
        {
            return Crossover(other, skipDistanceCalculation: false);
        }

        /// <summary>
        /// Order Crossover (OX) for TSP - correct implementation.
        /// OX preserves relative order from parent2 while keeping a contiguous segment from parent1.
        /// </summary>
        /// <param name="other">The other parent route.</param>
        /// <param name="skipDistanceCalculation">If true, doesn't calculate distance (for parallel GA).</param>
        public Route Crossover(Route other, bool skipDistanceCalculation)
        {
            // Order Crossover (OX) for TSP - correct implementation
            // OX preserves relative order from parent2 while keeping a contiguous segment from parent1
            int size = Cities.Count;
            if (size <= 1)
            {
                return new Route(this, skipDistanceCalculation);
            }
            
            // Select a contiguous segment from parent1 (this route)
            int start = _random.Next(size);
            int end = _random.Next(start, size);
            
            var offspring = new int[size];
            // Initialize with -1 to mark empty positions
            Array.Fill(offspring, -1);
            var segmentSet = new HashSet<int>();
            
            // Step 1: Copy the selected segment from parent1 to offspring at same positions
            for (int i = start; i <= end; i++)
            {
                offspring[i] = Cities[i];
                segmentSet.Add(Cities[i]);
            }
            
            // Step 2: Fill remaining positions with cities from parent2 in their original order
            // Start filling from position after the segment, wrapping around if needed
            int fillPos = (end + 1) % size;
            foreach (int city in other.Cities)
            {
                // Skip cities that are already in the segment from parent1
                if (!segmentSet.Contains(city))
                {
                    // Find next empty position (skip positions already filled by segment)
                    while (offspring[fillPos] != -1)
                    {
                        fillPos = (fillPos + 1) % size;
                    }
                    
                    offspring[fillPos] = city;
                    fillPos = (fillPos + 1) % size;
                }
            }
            
            // Create route without calculating distance if requested (for parallel GA)
            var newRoute = new Route(_cities, _random, offspring.ToList(), skipDistanceCalculation);
            
            return newRoute;
        }

        public Route InversionMutation()
        {
            // Inversion mutation: reverse a segment of the route
            int start = _random.Next(Cities.Count);
            int length = _random.Next(2, Cities.Count / 2);
            
            var newCities = new List<int>(Cities);
            for (int i = 0; i < length; i++)
            {
                int pos1 = (start + i) % Cities.Count;
                int pos2 = (start + length - 1 - i) % Cities.Count;
                
                if (pos1 != pos2)
                {
                    int temp = newCities[pos1];
                    newCities[pos1] = newCities[pos2];
                    newCities[pos2] = temp;
                }
            }
            
            var newRoute = new Route(_cities, _random)
            {
                Cities = newCities
            };
            newRoute.CalculateDistance();
            
            return newRoute;
        }

        public Route ScrambleMutation()
        {
            // Scramble mutation: randomly reorder a segment
            int start = _random.Next(Cities.Count);
            int length = _random.Next(2, Cities.Count / 3);
            
            var segment = new List<int>();
            for (int i = 0; i < length; i++)
            {
                int pos = (start + i) % Cities.Count;
                segment.Add(Cities[pos]);
            }
            
            // Shuffle segment
            for (int i = segment.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                int temp = segment[i];
                segment[i] = segment[j];
                segment[j] = temp;
            }
            
            var newCities = new List<int>(Cities);
            for (int i = 0; i < length; i++)
            {
                int pos = (start + i) % Cities.Count;
                newCities[pos] = segment[i];
            }
            
            var newRoute = new Route(_cities, _random)
            {
                Cities = newCities
            };
            newRoute.CalculateDistance();
            
            return newRoute;
        }

        public override string ToString()
        {
            return $"Route: [{string.Join(" -> ", Cities)}] Distance: {TotalDistance:F2}";
        }

        public string GetFormattedRoute()
        {
            var cityNames = Cities.Select(i => $"City{i}").ToList();
            return string.Join(" -> ", cityNames) + $" -> City{Cities[0]}";
        }
    }
} 