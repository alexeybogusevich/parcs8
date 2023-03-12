using Microsoft.Extensions.Configuration;
using Parcs.Daemon.Server;
using Parcs.TCP.Daemon.Configuration;
using Parcs.TCP.Daemon.Services;
using System.Net;

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

        var factory = new SignalHandlerFactory();
        var server = new DaemonServer(IPAddress.Any, nodeConfiguration.Port, factory);

        Console.Write("Server starting...");
        server.Start();
        Console.WriteLine("Done!");

        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        Console.WriteLine("Stopping the server...");
        server.Stop();
        Console.WriteLine("Done!");
    }
}