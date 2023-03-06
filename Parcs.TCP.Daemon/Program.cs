using Microsoft.Extensions.Configuration;
using Parcs.Core;
using Parcs.TCP.Daemon.Configuration;
using Parcs.TCP.Daemon.EntryPoint;

class Program
{
    static void Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory))
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var nodeConfiguration = configuration
            .GetSection(NodeConfiguration.SectionName)
            .Get<NodeConfiguration>();

        Console.WriteLine($"Server address: {nodeConfiguration.IpAddress}");
        Console.WriteLine($"Server port: {nodeConfiguration.Port}");
        Console.WriteLine();

        var server = new DaemonServer(nodeConfiguration.IpAddress, nodeConfiguration.Port);
        server.Start();

        Console.Write("Server starting...");
        server.Start();
        Console.WriteLine("Done!");

        var channel = new Channel(server);

        double a = channel.ReadDouble();
        double b = channel.ReadDouble();
        double h = channel.ReadDouble();
        var func = new Func<double, double>(x => Math.Cos(x));

        double res = Integral(a, b, h, func);
        channel.WriteData(res);

        // Disconnect the client
        Console.ReadLine();

        Console.Write("Client disconnecting...");
        client.DisconnectAndStop();
        Console.WriteLine("Done!");
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