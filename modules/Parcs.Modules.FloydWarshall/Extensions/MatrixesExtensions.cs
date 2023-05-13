using Parcs.Modules.FloydWarshall.Models;

namespace Parcs.Modules.FloydWarshall.Extensions
{
    public static class MatrixesExtensions
    {
        public static void FillWithRandomDistances(this Matrix matrix, int maxDistance)
        {
            var random = new Random();

            for (int i = 0; i < matrix.Height; ++ i)
            {
                for (int j = 0; j < matrix.Width; ++j)
                {
                    if (i == j)
                    {
                        matrix[i, j] = 0;
                        continue;
                    }

                    if (random.NextDouble() >= 0.5)
                    {
                        matrix[i, j] = int.MaxValue;
                        continue;
                    }

                    matrix[i, j] = random.Next(maxDistance);
                }
            }
        }
    }
}