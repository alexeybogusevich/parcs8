using Microsoft.Extensions.Configuration;
using Parcs.TCP.Daemon.Configuration;
using Parcs.TCP.Daemon.EntryPoint;

class Program
{
    static void Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var hostConfiguration = configuration
            .GetSection(HostConfiguration.SectionName)
            .Get<HostConfiguration>();

        Console.WriteLine($"Host Server address: {hostConfiguration.IpAddress}");
        Console.WriteLine($"Host Server port: {hostConfiguration.Port}");
        Console.WriteLine();

        var client = new DaemonClient(hostConfiguration.IpAddress, hostConfiguration.Port);

        Console.Write("Client connecting...");
        client.ConnectAsync();
        Console.WriteLine("Done!");

        Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

        // Perform text input
        for (; ; )
        {
            string line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            // Disconnect the client
            if (line == "!")
            {
                Console.Write("Client disconnecting...");
                client.DisconnectAsync();
                Console.WriteLine("Done!");
                continue;
            }

            // Send the entered text to the chat server
            client.SendAsync(line);
        }

        // Disconnect the client
        Console.Write("Client disconnecting...");
        client.DisconnectAndStop();
        Console.WriteLine("Done!");
    }
}