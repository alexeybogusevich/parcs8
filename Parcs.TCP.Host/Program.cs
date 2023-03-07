using Microsoft.Extensions.Configuration;
using Parcs.Core;
using Parcs.TCP.Host.Configuration;
using Parcs.TCP.Host.Models;

class Program
{
    static void Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var daemonConfigurations = configuration
            .GetSection("Daemons")
            .Get<IEnumerable<DaemonConfiguration>>();

        Console.WriteLine("Configured daemons:");
        foreach (var daemon in daemonConfigurations)
        {
            Console.WriteLine($"IP address: {daemon.IpAddress}, Port: {daemon.Port}.");
        }
        Console.WriteLine();

        var hostInfo = new HostInfo(daemonConfigurations);

        double a = 0;
        double b = Math.PI / 2;
        double h = 0.00000001;

        var pointsNumber = hostInfo.MaximumPointsNumber;
        var channels = new IChannel[pointsNumber];
        var points = new IPoint[pointsNumber];

        for (int i = 0; i < pointsNumber; ++i)
        {
            points[i] = hostInfo.CreatePoint();
            channels[i] = points[i].CreateChannel();
            channels[i].ExecuteClass("Some funny class :)");
        }

        for (int i = 0; i < pointsNumber; ++i)
        {
            channels[i].WriteData(10.1D);
            channels[i].WriteData(true);
            channels[i].WriteData("Hello world");
            channels[i].WriteData((byte)1);
            channels[i].WriteData(123L);
            channels[i].WriteData(22);

            var job = new Job
            {
                StartDateUtc = DateTime.UtcNow,
                Status = JobStatus.InProgress,
                CreateDateUtc = DateTime.UtcNow.AddDays(-1),
                EndDateUtc = DateTime.UtcNow.AddDays(1),
                Id = Guid.NewGuid(),
            };

            channels[i].WriteObject(job);
        }
        DateTime time = DateTime.Now;
        Console.WriteLine("Waiting for result...");

        double res = 0;
        for (int i = pointsNumber - 1; i >= 0; --i)
        {
            res += channels[i].ReadDouble();
        }

        Console.WriteLine("Result found: res = {0}, time = {1}", res, Math.Round((DateTime.Now - time).TotalSeconds, 3));

        Console.ReadLine();
        Console.Write("Disconnecting the client...");

        for (int i = 0; i < pointsNumber; ++i)
        {
            points[i].Delete();
        }

        Console.WriteLine("Done!");
    }
}