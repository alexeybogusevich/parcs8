﻿using Parcs.Net;
using System.Text;

namespace Parcs.Modules.Integral
{
    public class MainModule : IMainModule
    {
        public async Task RunAsync(IArgumentsProvider argumentsProvider, IHostInfo hostInfo, CancellationToken cancellationToken = default)
        {
            var moduleOptions = argumentsProvider.Bind<ModuleOptions>();

            double a = 0;
            double b = Math.PI / 2;
            double h = moduleOptions.Precision ?? 0.00000001;

            var pointsNumber = argumentsProvider.GetBase().PointsNumber;

            if (pointsNumber > hostInfo.CanCreatePointsNumber)
            {
                throw new ArgumentException($"More points ({pointsNumber}) than allowed ({hostInfo.CanCreatePointsNumber}).");
            }

            var points = new IPoint[pointsNumber];
            var channels = new IChannel[pointsNumber];

            for (int i = 0; i < pointsNumber; ++i)
            {
                points[i] = await hostInfo.CreatePointAsync();
                channels[i] = await points[i].CreateChannelAsync();
                await points[i].ExecuteClassAsync<WorkerModule>();
            }

            double y = a;
            for (int i = 0; i < pointsNumber; ++i)
            {
                await channels[i].WriteDataAsync(y);
                await channels[i].WriteDataAsync(y + (b - a) / pointsNumber);
                await channels[i].WriteDataAsync(h);
                y += (b - a) / pointsNumber;
            }

            DateTime time = DateTime.Now;
            Console.WriteLine("Waiting for result...");

            double result = 0;
            for (int i = pointsNumber - 1; i >= 0; --i)
            {
                result += await channels[i].ReadDoubleAsync();
            }

            Console.WriteLine("Result found: res = {0}, time = {1}", result, Math.Round((DateTime.Now - time).TotalSeconds, 3));

            await hostInfo.GetOutputWriter().WriteToFileAsync(Encoding.UTF8.GetBytes(result.ToString()), "result.txt");

            for (int i = 0; i < pointsNumber; ++i)
            {
                await points[i].DeleteAsync();
            }
        }
    }
}