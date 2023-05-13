using Parcs.Modules.FloydWarshall.Models;
using Parcs.Net;

namespace Parcs.Modules.FloydWarshall.Parallel
{
    public class ParallelWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            var currentNumber = await moduleInfo.Parent.ReadIntAsync();
            var chunk = await moduleInfo.Parent.ReadObjectAsync<Matrix>();

            for (int k = 0; k < chunk.Width; k++)
            {
                List<int> currentRow;

                if (k >= currentNumber * chunk.Height && k < currentNumber * chunk.Height + chunk.Height)
                {
                    currentRow = chunk.Data[k % chunk.Height];
                    await moduleInfo.Parent.WriteObjectAsync(chunk.Data[k % chunk.Height]);
                }
                else
                {
                    currentRow = await moduleInfo.Parent.ReadObjectAsync<List<int>>();
                }

                for (int i = 0; i < chunk.Height; i++)
                {
                    for (int j = 0; j < chunk.Width; j++)
                    {
                        chunk[i, j] = MinWeight(chunk[i, j], chunk[i, k], currentRow[j]);
                    }
                }
            }

            await moduleInfo.Parent.WriteObjectAsync(chunk);
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