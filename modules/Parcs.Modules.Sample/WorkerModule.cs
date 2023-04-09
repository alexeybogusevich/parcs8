using Parcs.Modules.Sample.Models;
using Parcs.Net;

namespace Parcs.Modules.Sample
{
    public class WorkerModule : IWorkerModule
    {
        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            Console.WriteLine(await channel.ReadDoubleAsync());
            Console.WriteLine(await channel.ReadBooleanAsync());
            Console.WriteLine(await channel.ReadStringAsync());
            Console.WriteLine(await channel.ReadByteAsync());
            Console.WriteLine(await channel.ReadLongAsync());
            Console.WriteLine(await channel.ReadIntAsync());

            var sampleClass = await channel.ReadObjectAsync<SampleClass>();

            Console.WriteLine(sampleClass.Id);
            Console.WriteLine(sampleClass.Name);

            await channel.WriteDataAsync(1111.11D);
        }
    }
}