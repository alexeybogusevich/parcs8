using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal class ExecuteClassSignalHandler : ISignalHandler
    {
        public void Handle(byte[] buffer, long offset, long size, IChannel channel)
        {
            var className = channel.ReadString();
            //var inputFile = ...

            double a = channel.ReadDouble();
            double b = channel.ReadDouble();
            double h = channel.ReadDouble();
            var func = new Func<double, double>(x => Math.Cos(x));

            double res = Integral(a, b, h, func);
            channel.WriteData(res);
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