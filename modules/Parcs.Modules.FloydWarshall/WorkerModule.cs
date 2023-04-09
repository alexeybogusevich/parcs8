using Parcs.Net;

namespace Parcs.Modules.FloydWarshall
{
    public class WorkerModule : IWorkerModule
    {
        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var number = await channel.ReadIntAsync();
            Console.WriteLine($"Current number {number}");
            var chunk = await channel.ReadObjectAsync<int[][]>();

            int n = chunk[0].Length; //width
            int c = chunk.Length; //height
            Console.WriteLine($"Chunk {c}x{n}");

            for (int k = 0; k < n; k++) // ->
            {
                int[] currentRow;

                if (k >= number * c && k < number * c + c)
                {
                    currentRow = chunk[k % c]; // iterate through all chunk rows
                    await channel.WriteObjectAsync(chunk[k % c]);
                }
                else
                {
                    currentRow = await channel.ReadObjectAsync<int[]>();
                }

                for (int i = 0; i < c; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        chunk[i][j] = MinWeight(chunk[i][j], chunk[i][k], currentRow[j]);
                    }
                }
            }

            await channel.WriteObjectAsync(chunk);
            Console.WriteLine("Done!");
        }

        static int MinWeight(int a, int b, int c)
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
    }
}