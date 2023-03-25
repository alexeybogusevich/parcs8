using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Extensions;
using Parcs.Shared.Models;
using Parcs.TCP.Daemon.Configuration;
using Parcs.TCP.Daemon.Services.Interfaces;
using System.Net;
using System.Net.Sockets;

var serviceProvider = new ServiceCollection()
    .ConfigureServices()
    .BuildServiceProvider();

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

    var signalHandlerFactory = serviceProvider.GetRequiredService<ISignalHandlerFactory>();

    for (;;)
    {
        Console.Write("Waiting for a connection... ");

        using var tcpClient = await tcpListener.AcceptTcpClientAsync();
        using var channel = new Channel(tcpClient.GetStream());

        var signal = await channel.ReadSignalAsync();
        var signalHandler = signalHandlerFactory.Create(signal);
        await signalHandler.HandleAsync(channel);
    }
}
finally
{
    Console.WriteLine("Stopping the server...");
    tcpListener.Stop();
    Console.WriteLine("Done!");
}