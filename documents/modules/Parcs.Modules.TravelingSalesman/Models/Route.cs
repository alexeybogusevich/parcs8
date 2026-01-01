using System;
using System.Collections.Generic;
using System.Linq;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public class Route
    {
        private readonly List<City> _cities;
        private readonly Random _random;
        
        public List<int> Cities { get; private set; }
        public double TotalDistance { get; private set; }

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
            // Swap mutation: randomly swap two cities
            int index1 = _random.Next(Cities.Count);
            int index2 = _random.Next(Cities.Count);
            
            if (index1 != index2)
            {
                int temp = Cities[index1];
                Cities[index1] = Cities[index2];
                Cities[index2] = temp;
                
                CalculateDistance();
            }
        }

        public Route Crossover(Route other)
        {
            // Order Crossover (OX) for TSP
            int size = Cities.Count;
            int start = _random.Next(size);
            int length = _random.Next(1, size);
            
            var offspring = new int[size];
            var used = new HashSet<int>();
            
            // Copy segment from parent1
            for (int i = 0; i < length; i++)
            {
                int pos = (start + i) % size;
                offspring[pos] = Cities[pos];
                used.Add(Cities[pos]);
            }
            
            // Fill remaining positions with cities from parent2 in order
            int currentPos = (start + length) % size;
            foreach (int city in other.Cities)
            {
                if (!used.Contains(city))
                {
                    offspring[currentPos] = city;
                    used.Add(city);
                    currentPos = (currentPos + 1) % size;
                }
            }
            
            var newRoute = new Route(_cities, _random)
            {
                Cities = offspring.ToList()
            };
            newRoute.CalculateDistance();
            
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