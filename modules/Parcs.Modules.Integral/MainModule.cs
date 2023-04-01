using Parcs.Net;

namespace Parcs.Modules.Integral
{
    public class MainModule : IMainModule
    {
        public string Name => "Main Integral Module";

        public async Task RunAsync(IHostInfo hostInfo, IInputReader inputReader, IOutputWriter outputWriter, CancellationToken cancellationToken = default)
        {
            double a = 0;
            double b = Math.PI / 2;
            double h = 0.00000001;

            var pointsNumber = hostInfo.AvailablePointsNumber;
            var points = new IPoint[pointsNumber];
            var channels = new IChannel[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync(cancellationToken);
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            double y = a;
            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(y, cancellationToken);
                await channels[i].WriteDataAsync(y + (b - a) / pointsNumber, cancellationToken);
                await channels[i].WriteDataAsync(h, cancellationToken);
                y += (b - a) / pointsNumber;
            }

            DateTime time = DateTime.Now;
            Console.WriteLine("Waiting for result...");

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync(cancellationToken);
            }

            Console.WriteLine("Result found: res = {0}, time = {1}", result, Math.Round((DateTime.Now - time).TotalSeconds, 3));

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }
        }
    }
}