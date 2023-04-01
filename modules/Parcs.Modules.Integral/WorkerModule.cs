using Parcs.Net;

namespace Parcs.Modules.Integral
{
    public class WorkerModule : IWorkerModule
    {
        public string Name => "Worker Integral Module";

        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            double a = await channel.ReadDoubleAsync();
            double b = await channel.ReadDoubleAsync();
            double h = await channel.ReadDoubleAsync();

            var func = new Func<double, double>(Math.Cos);

            double result = Integral(a, b, h, func);

            await channel.WriteDataAsync(result);
        }

        private static double Integral(double a, double b, double h, Func<double, double> func)
        {
            int N = (int)((b - a) / h);

            double res = 0;
            for (int j = 1; j <= N; ++j)
            {
                double x = a + (2 * j - 1) * h / 2;
                res += func(x);
            }

            return res * h;
        }
    }
}