using System;

namespace Parcs.Modules.TravelingSalesman.Models
{
    public class City
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public City(int id, double x, double y)
        {
            Id = id;
            X = x;
            Y = y;
        }

        public City()
        {
            Id = 0;
            X = 0;
            Y = 0;
        }

        public double DistanceTo(City other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            double deltaX = X - other.X;
            double deltaY = Y - other.Y;
            
            // Euclidean distance
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public double ManhattanDistanceTo(City other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public double ChebyshevDistanceTo(City other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
        }

        public override string ToString()
        {
            return $"City{Id}({X:F2}, {Y:F2})";
        }

        public override bool Equals(object obj)
        {
            if (obj is City other)
            {
                return Id == other.Id && 
                       Math.Abs(X - other.X) < 1e-9 && 
                       Math.Abs(Y - other.Y) < 1e-9;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, X, Y);
        }

        public static City operator -(City a, City b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

            return new City(-1, a.X - b.X, a.Y - b.Y);
        }

        public static City operator +(City a, City b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));

            return new City(-1, a.X + b.X, a.Y + b.Y);
        }
    }
} 