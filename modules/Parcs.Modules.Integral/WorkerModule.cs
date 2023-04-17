using Parcs.Net;

namespace Parcs.Modules.Integral
{
    public class WorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            double a = await moduleInfo.Parent.ReadDoubleAsync();
            double b = await moduleInfo.Parent.ReadDoubleAsync();
            double h = await moduleInfo.Parent.ReadDoubleAsync();

            var function = new Func<double, double>(Math.Cos);

            double result = Integral(a, b, h, function);

            await moduleInfo.Parent.WriteDataAsync(result);
        }

        private static double Integral(double a, double b, double h, Func<double, double> function)
        {
            int N = (int)((b - a) / h);

            double res = 0;
            for (int j = 1; j <= N; ++j)
            {
                double x = a + (2 * j - 1) * h / 2;
                res += function(x);
            }

            return res * h;
        }
    }
}