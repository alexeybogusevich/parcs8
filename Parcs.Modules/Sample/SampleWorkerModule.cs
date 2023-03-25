using Parcs.Net;

namespace Parcs.Modules.Sample
{
    public class SampleWorkerModule : IWorkerModule
    {
        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            Console.WriteLine(await channel.ReadDoubleAsync(cancellationToken));
            Console.WriteLine(await channel.ReadBooleanAsync(cancellationToken));
            Console.WriteLine(await channel.ReadStringAsync(cancellationToken));
            Console.WriteLine(await channel.ReadByteAsync(cancellationToken));
            Console.WriteLine(await channel.ReadLongAsync(cancellationToken));
            Console.WriteLine(await channel.ReadIntAsync(cancellationToken));

            await channel.WriteDataAsync(1111.11D, cancellationToken);
        }
    }
}