using Microsoft.Extensions.Configuration;
using Parcs.Core;
using Parcs.TCP.Daemon.Configuration;
using Parcs.TCP.Daemon.Services;
using System.Net;
using System.Net.Sockets;

class Program
{
    static async Task Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var nodeConfiguration = configuration
            .GetSection(NodeConfiguration.SectionName)
            .Get<NodeConfiguration>();
        
        Console.WriteLine($"Server port: {nodeConfiguration.Port}");
        Console.WriteLine();

        var ipEndpoint = new IPEndPoint(IPAddress.Any, nodeConfiguration.Port);
        var tcpListener = new TcpListener(ipEndpoint);

        try
        {
            Console.Write("Server starting...");
            tcpListener.Start();
            Console.WriteLine("Done!");

            for (;;)
            {
                Console.Write("Waiting for a connection... ");

                using var tcpClient = await tcpListener.AcceptTcpClientAsync();
                await using var networkStream = tcpClient.GetStream();
                var channel = new Channel(networkStream);

                var signal = await channel.ReadSignalAsync();
                var signalHandler = new SignalHandlerFactory().Create(signal);
                await signalHandler.HandleAsync(channel);
            }
        }
        finally
        {
            Console.WriteLine("Stopping the server...");
            tcpListener.Stop();
            Console.WriteLine("Done!");
        }
    }
}