using Parcs.Net;

namespace Parcs.Modules.FloydWarshall.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var number = await moduleInfo.Parent.ReadIntAsync();
            Console.WriteLine($"Current number {number}");
            var chunk = await moduleInfo.Parent.ReadObjectAsync<List<List<int>>>();

            int n = chunk[0].Count; //width
            int c = chunk.Count; //height
            Console.WriteLine($"Chunk {c}x{n}");

            for (int k = 0; k < n; k++) // ->
            {
                var currentRow = new List<int>();

                if (k >= number * c && k < number * c + c)
                {
                    currentRow = chunk[k % c]; // iterate through all chunk rows
                    await moduleInfo.Parent.WriteObjectAsync(chunk[k % c]);
                }
                else
                {
                    currentRow = await moduleInfo.Parent.ReadObjectAsync<List<int>>();
                }

                for (int i = 0; i < c; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        chunk[i][j] = MinWeight(chunk[i][j], chunk[i][k], currentRow[j]);
                    }
                }
            }

            await moduleInfo.Parent.WriteObjectAsync(chunk);
            Console.WriteLine("Done!");
        }

        static int MinWeight(int a, int b, int c)
        {
            if (a != int.MaxValue)
            {
                if (b != int.MaxValue && c != int.MaxValue)
                {
                    return Math.Min(a, b + c);
                }
                else
                {
                    return a;
                }
            }

            if (b == int.MaxValue || c == int.MaxValue)
            {
                return a;
            }
            else
            {
                return b + c;
            }
        }
    }
}