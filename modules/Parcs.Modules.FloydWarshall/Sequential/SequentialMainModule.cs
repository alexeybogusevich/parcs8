using Parcs.Net;
using System.Text;

namespace Parcs.Modules.FloydWarshall.Sequential
{
    public class SequentialMainModule : IModule
    {
        public Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var options = moduleInfo.ArgumentsProvider.Bind<ModuleOptions>();


        }

        private static int[][] Floyd(int[][] m)
        {
            int[][] result = (int[][])m.Clone();
            int rowLength = result.Length;

            for (int k = 0; k < rowLength; k++)
            {
                for (int i = 0; i < rowLength; i++)
                {
                    for (int j = 0; j < rowLength; j++)
                    {
                        result[i][j] = MinWeight(result[i][j], result[i][k], result[k][j]);
                    }
                }
            }

            return result;
        }

        private static int MinWeight(int a, int b, int c)
        {
            if (a != int.MaxValue)
            {
                if (b != int.MaxValue && c != int.MaxValue)
                    return Math.Min(a, b + c);
                else
                    return a;
            }
            else
            {
                if (b == int.MaxValue || c == int.MaxValue)
                    return a;
                else
                    return b + c;
            }
        }

        private static void SaveMatrix(string filename, int[][] m)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < m.Length; i++)
            {
                for (int j = 0; j < m.Length; j++)
                {
                    sb.Append(m[i][j]);
                    if (j != m.Length - 1)
                    {
                        sb.Append(' ');
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(filename, sb.ToString());
        }

        private static int[][] ReadMatrix()
        {
            Console.WriteLine("Matrix size: ");
            string input = Console.ReadLine();
            var m = int.Parse(input);
            var result = new int[m][];
            for (int i = 0; i < m; i++)
            {
                result[i] = Console.ReadLine()
                    .Replace("-1", int.MaxValue.ToString())
                    .Split(' ')
                    .Select(a => int.Parse(a))
                    .ToArray();
            }
            return result;
        }

        private static void PrintMatrix(int[][] m)
        {
            int rowLength = m.Length;

            for (int i = 0; i < rowLength; i++)
            {
                for (int j = 0; j < m[i].Length; j++)
                {
                    Console.Write(m[i][j] + " ");
                }
                Console.WriteLine();
            }
        }
    }
}